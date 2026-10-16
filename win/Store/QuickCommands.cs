using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using PixShell.Logging;

namespace PixShell.Store;

/// <summary>快捷命令参数（对齐 mac Store/QuickCommands.swift 的 QuickParam）。</summary>
public class QuickCommandParam
{
    public string Name { get; set; } = "";
    public string? DefaultValue { get; set; }
    public bool? Required { get; set; }
}

/// <summary>快捷命令（对齐 mac QuickCommand：id/name/group/command/params）。</summary>
public class QuickCommand
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Group { get; set; } = "默认";
    public string Command { get; set; } = "";
    public List<QuickCommandParam>? Params { get; set; }
    public bool? AutoReturn { get; set; } = true;

    /// <summary>同一分组内的显示顺序。旧文件缺少该字段时按原 JSON 数组顺序兼容。</summary>
    [JsonPropertyName("sortOrder")]
    public int? SortOrder { get; set; }
}

/// <summary>
/// 快捷命令持久化 + 分组/渲染逻辑（1:1 移植 mac Store/QuickCommands.swift 的 QuickCommandStore）：
/// 存 `%APPDATA%\PixShell\quick-commands.json`；首次运行（文件不存在/为空）用与 mac 完全一致的
/// 默认集播种（老仓库 DEMO_QUICK_COMMANDS）。
/// </summary>
public sealed class QuickCommandStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _path;
    private readonly string _groupsPath;

    public List<QuickCommand> Commands { get; private set; } = new();

    /// <summary>显式分组表。**必须单独存**：分组光靠命令的 Group 字段派生的话，
    /// 新建一个还没放命令的空分组会立刻消失。这里记住用户建过的空分组
    /// （对齐 mac QuickCommandStore.emptyGroups / quick-command-groups.json）。</summary>
    public List<string> EmptyGroups { get; private set; } = new();

    /// <summary>
    /// 默认使用应用数据目录；测试可传入独立目录，避免读取或改写用户实际快捷命令。
    /// </summary>
    public QuickCommandStore(string? appDir = null)
    {
        var dir = string.IsNullOrWhiteSpace(appDir) ? HostStore.AppDir : appDir;
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "quick-commands.json");
        _groupsPath = Path.Combine(dir, "quick-command-groups.json");
        Load();
    }

    private void Load()
    {
        LoadGroups();
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var list = JsonSerializer.Deserialize<List<QuickCommand>>(json, JsonOpts);
                if (list is { Count: > 0 })
                {
                    Commands = list;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"读取 quick-commands.json 失败，使用默认集: {ex.Message}", "quickcmd");
        }
        Commands = Defaults();
        Save();
    }

    private void LoadGroups()
    {
        try
        {
            if (File.Exists(_groupsPath))
            {
                var list = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_groupsPath), JsonOpts);
                if (list != null) EmptyGroups = list;
            }
        }
        catch (Exception ex) { Log.Warn($"读取 quick-command-groups.json 失败: {ex.Message}", "quickcmd"); }
    }

    public void Save()
    {
        try { File.WriteAllText(_path, JsonSerializer.Serialize(Commands, JsonOpts)); }
        catch (Exception ex) { Log.Warn($"写入 quick-commands.json 失败: {ex.Message}", "quickcmd"); }
    }

    private void SaveGroups()
    {
        try { File.WriteAllText(_groupsPath, JsonSerializer.Serialize(EmptyGroups, JsonOpts)); }
        catch (Exception ex) { Log.Warn($"写入 quick-command-groups.json 失败: {ex.Message}", "quickcmd"); }
    }

    /// <summary>分组 = 显式建过的 ∪ 命令里出现过的（都按首次出现保序，对齐 mac groups()）。</summary>
    public List<string> Groups()
    {
        var seen = new HashSet<string>();
        var outp = new List<string>();
        foreach (var g in EmptyGroups)
            if (seen.Add(g)) outp.Add(g);
        foreach (var c in Commands)
        {
            var g = string.IsNullOrEmpty(c.Group) ? "默认" : c.Group;
            if (seen.Add(g)) outp.Add(g);
        }
        return outp;
    }

    /// <summary>新建分组。已存在返回 false（调用方据此提示）。</summary>
    public bool AddGroup(string name)
    {
        var n = name.Trim();
        if (n.Length == 0 || Groups().Contains(n)) return false;
        EmptyGroups.Add(n);
        SaveGroups();
        return true;
    }

    /// <summary>删除分组：里面的命令一并挪回「默认」，**不连带删命令**（对齐 mac removeGroup）。</summary>
    public void RemoveGroup(string name)
    {
        EmptyGroups.RemoveAll(g => g == name);
        foreach (var c in Commands)
            if ((string.IsNullOrEmpty(c.Group) ? "默认" : c.Group) == name) c.Group = "默认";
        NormalizeSortOrders();
        SaveGroups();
        Save();
    }

    /// <summary>重命名分组：分组名存在每条命令里，故逐条改（对齐 mac renameGroup）。</summary>
    public void RenameGroup(string oldName, string newName)
    {
        var n = newName.Trim();
        if (n.Length == 0 || n == oldName || Groups().Contains(n)) return;
        var i = EmptyGroups.IndexOf(oldName);
        if (i >= 0) EmptyGroups[i] = n;
        foreach (var c in Commands)
            if ((string.IsNullOrEmpty(c.Group) ? "默认" : c.Group) == oldName) c.Group = n;
        NormalizeSortOrders();
        SaveGroups();
        Save();
    }

    private static string GroupOf(QuickCommand command) =>
        string.IsNullOrEmpty(command.Group) ? "默认" : command.Group;

    /// <summary>把一条命令移到某个分组，并追加到目标组末尾。</summary>
    public void Move(string id, string group)
    {
        var sourceIndex = Commands.FindIndex(x => x.Id == id);
        if (sourceIndex < 0) return;

        var command = Commands[sourceIndex];
        var targetGroup = string.IsNullOrEmpty(group) ? "默认" : group;
        if (GroupOf(command) == targetGroup) return;

        command.Group = targetGroup;
        command.SortOrder = NextSortOrder(targetGroup);
        NormalizeSortOrders();
        Save();
    }

    public bool CanMoveUp(string id) => FindNeighborIndex(id, -1) >= 0;

    public bool CanMoveDown(string id) => FindNeighborIndex(id, 1) >= 0;

    /// <summary>仅在同一分组内交换相邻命令，并更新持久化 sortOrder。</summary>
    public bool MoveUp(string id) => MoveWithinGroup(id, -1);

    /// <summary>仅在同一分组内交换相邻命令，并更新持久化 sortOrder。</summary>
    public bool MoveDown(string id) => MoveWithinGroup(id, 1);

    /// <summary>
    /// 将命令拖拽到同组另一命令之前；<paramref name="beforeId"/> 为 null 时追加到该组末尾。
    /// 该操作不允许借拖拽改变分组，跨组移动仍必须使用 <see cref="Move"/>。
    /// </summary>
    public bool ReorderBefore(string id, string? beforeId)
    {
        if (beforeId == id) return false;
        var index = Commands.FindIndex(x => x.Id == id);
        if (index < 0) return false;

        var group = GroupOf(Commands[index]);
        if (beforeId != null)
        {
            var before = Commands.FindIndex(x => x.Id == beforeId);
            if (before < 0 || GroupOf(Commands[before]) != group) return false;
        }

        NormalizeSortOrders();
        var ordered = OrderedIndices(group);
        if (!ordered.Remove(index)) return false;

        if (beforeId == null)
        {
            ordered.Add(index);
        }
        else
        {
            var beforeIndex = Commands.FindIndex(x => x.Id == beforeId);
            var insertAt = ordered.IndexOf(beforeIndex);
            if (insertAt < 0) return false;
            ordered.Insert(insertAt, index);
        }

        ApplyOrder(ordered);
        Save();
        return true;
    }

    private bool MoveWithinGroup(string id, int direction)
    {
        var index = Commands.FindIndex(x => x.Id == id);
        NormalizeSortOrders();
        var neighbor = FindNeighborIndex(id, direction);
        if (index < 0 || neighbor < 0) return false;

        (Commands[index].SortOrder, Commands[neighbor].SortOrder) =
            (Commands[neighbor].SortOrder, Commands[index].SortOrder);
        Save();
        return true;
    }

    private int FindNeighborIndex(string id, int direction)
    {
        var index = Commands.FindIndex(x => x.Id == id);
        if (index < 0) return -1;

        var ordered = OrderedIndices(GroupOf(Commands[index]));
        var position = ordered.IndexOf(index);
        var neighborPosition = position + direction;
        return neighborPosition >= 0 && neighborPosition < ordered.Count ? ordered[neighborPosition] : -1;
    }

    public List<QuickCommand> List(string? group) =>
        string.IsNullOrEmpty(group)
            ? Commands
            : OrderedIndices(group).Select(i => Commands[i]).ToList();

    public void Upsert(QuickCommand c)
    {
        var i = Commands.FindIndex(x => x.Id == c.Id);
        if (i >= 0)
        {
            var oldGroup = GroupOf(Commands[i]);
            var newGroup = GroupOf(c);
            c.SortOrder = oldGroup == newGroup ? Commands[i].SortOrder : NextSortOrder(newGroup);
            Commands[i] = c;
        }
        else
        {
            c.SortOrder = NextSortOrder(GroupOf(c));
            Commands.Add(c);
        }
        NormalizeSortOrders();
        Save();
    }

    /// <summary>
    /// 合并备份中的快捷命令。语义与主机备份恢复一致：同 ID 覆盖为备份版本，
    /// 缺失的 ID 新增，但不删除仅存在于本机的命令。整个批次只写盘一次。
    /// </summary>
    public int MergeFromBackup(IEnumerable<QuickCommand>? commands)
    {
        if (commands == null) return 0;

        var merged = 0;
        foreach (var command in commands)
        {
            if (command == null) continue;
            if (string.IsNullOrWhiteSpace(command.Id)) command.Id = Guid.NewGuid().ToString();
            if (string.IsNullOrWhiteSpace(command.Group)) command.Group = "默认";

            var index = Commands.FindIndex(existing => existing.Id == command.Id);
            if (index >= 0) Commands[index] = command;
            else Commands.Add(command);
            merged++;
        }

        if (merged > 0)
        {
            NormalizeSortOrders();
            Save();
        }
        return merged;
    }

    public void Delete(string id)
    {
        Commands.RemoveAll(c => c.Id == id);
        NormalizeSortOrders();
        Save();
    }

    private List<int> OrderedIndices(string group) => Commands
        .Select((command, index) => (command, index))
        .Where(x => GroupOf(x.command) == group)
        .OrderBy(x => x.command.SortOrder ?? int.MaxValue)
        .ThenBy(x => x.index)
        .Select(x => x.index)
        .ToList();

    private int NextSortOrder(string group)
    {
        var orders = Commands.Where(c => GroupOf(c) == group && c.SortOrder.HasValue)
            .Select(c => c.SortOrder!.Value);
        return orders.Any() ? orders.Max() + 1 : Commands.Count(c => GroupOf(c) == group);
    }

    /// <summary>为旧 JSON 和异常/重复的排序值生成连续的同组顺序。</summary>
    private void NormalizeSortOrders()
    {
        foreach (var group in Commands.Select(GroupOf).Distinct())
            ApplyOrder(OrderedIndices(group));
    }

    private void ApplyOrder(IReadOnlyList<int> indices)
    {
        for (var position = 0; position < indices.Count; position++)
            Commands[indices[position]].SortOrder = position;
    }

    /// <summary>用给定取值 + 参数默认值渲染模板（对齐 mac render）。</summary>
    public string Render(QuickCommand c, IReadOnlyDictionary<string, string>? paramValues = null)
    {
        var outp = c.Command;
        if (paramValues != null)
            foreach (var kv in paramValues) outp = outp.Replace("${" + kv.Key + "}", kv.Value);
        foreach (var p in c.Params ?? new List<QuickCommandParam>())
        {
            if (p.DefaultValue != null && outp.Contains("${" + p.Name + "}"))
                outp = outp.Replace("${" + p.Name + "}", p.DefaultValue);
        }
        return outp;
    }

    /// <summary>老仓库 DEMO_QUICK_COMMANDS 同款默认集（与 mac QuickCommandStore.defaults 逐条对齐）。</summary>
    public static List<QuickCommand> Defaults() => new()
    {
        new QuickCommand { Id = "qc1", Name = "磁盘", Group = "系统", Command = "df -h" },
        new QuickCommand { Id = "qc2", Name = "内存", Group = "系统", Command = "free -m" },
        new QuickCommand { Id = "qc3", Name = "进程TOP", Group = "系统", Command = "ps aux --sort=-%cpu | head -20" },
        new QuickCommand { Id = "qc7", Name = "端口监听", Group = "系统", Command = "ss -tulnp" },
        new QuickCommand { Id = "qc4", Name = "Nginx 重载", Group = "服务", Command = "sudo systemctl reload nginx" },
        new QuickCommand
        {
            Id = "qc5", Name = "看日志", Group = "服务", Command = "sudo tail -n ${lines} -f ${file}",
            Params = new List<QuickCommandParam>
            {
                new QuickCommandParam { Name = "lines", DefaultValue = "100", Required = false },
                new QuickCommandParam { Name = "file", DefaultValue = "/var/log/nginx/error.log", Required = true },
            }
        },
        new QuickCommand
        {
            Id = "qc6", Name = "Docker PS", Group = "容器",
            Command = "docker ps --format \"table {{.Names}}\\t{{.Status}}\\t{{.Ports}}\""
        },
        // Rust 工具链：远端跑 cargo 的常用动作（与 mac QuickCommandStore.defaults 对齐）。
        new QuickCommand
        {
            Id = "rs1", Name = "cargo build", Group = "Rust", Command = "cargo build ${flags}",
            Params = new List<QuickCommandParam>
            {
                new QuickCommandParam { Name = "flags", DefaultValue = "--release", Required = false },
            }
        },
        new QuickCommand
        {
            Id = "rs2", Name = "cargo test", Group = "Rust", Command = "cargo test ${args}",
            Params = new List<QuickCommandParam>
            {
                new QuickCommandParam { Name = "args", DefaultValue = "", Required = false },
            }
        },
        new QuickCommand { Id = "rs3", Name = "cargo clippy", Group = "Rust", Command = "cargo clippy --all-targets -- -D warnings" },
        new QuickCommand { Id = "rs4", Name = "cargo fmt 检查", Group = "Rust", Command = "cargo fmt --all -- --check" },
        new QuickCommand
        {
            Id = "rs5", Name = "cargo run", Group = "Rust", Command = "cargo run ${args}",
            Params = new List<QuickCommandParam>
            {
                new QuickCommandParam { Name = "args", DefaultValue = "", Required = false },
            }
        },
        new QuickCommand { Id = "rs6", Name = "工具链版本", Group = "Rust", Command = "rustc -Vv; cargo -V; rustup show active-toolchain 2>/dev/null" },
    };
}

/// <summary>发送目标（对齐 mac SendTarget：当前会话 / 所有已连接会话 / 指定会话下标）。</summary>
public enum SendTargetKind { Current, AllConnected, Session }

public readonly struct SendTarget
{
    public SendTargetKind Kind { get; }
    public int SessionIndex { get; }

    private SendTarget(SendTargetKind kind, int sessionIndex)
    {
        Kind = kind;
        SessionIndex = sessionIndex;
    }

    public static SendTarget Current { get; } = new(SendTargetKind.Current, -1);
    public static SendTarget AllConnected { get; } = new(SendTargetKind.AllConnected, -1);
    public static SendTarget Session(int index) => new(SendTargetKind.Session, index);
}
