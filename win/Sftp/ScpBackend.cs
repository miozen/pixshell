using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PixShell.Logging;
using Renci.SshNet;

namespace PixShell.Sftp;

/// <summary>
/// Dropbear 原生 SCP + Shell exec 文件后端。
/// 不依赖 openssh-sftp-server，纯 Dropbear 即可用。
/// 列表用 shell ls -la，上传下载走 SSH.NET ScpClient，增删改用 shell exec。
/// </summary>
public class ScpBackend
{
    private readonly TerminalSession _session;
    private bool _connected;

    public bool Connected => _connected;
    public string WorkingDirectory => "/";

    public ScpBackend(TerminalSession session)
    {
        _session = session;
        _connected = true;
    }

    // ==================== List ====================

    public List<FsRow> ListDirectory(string path)
    {
        var raw = _session.ExecAsync($"LC_ALL=C ls -la {EscapeArg(path)} 2>&1; echo __PIXSHELL_RC:$?").GetAwaiter().GetResult();
        var (rc, body) = SftpTransfer.ParseRemoteRC(raw);
        if (rc != 0)
        {
            var reason = string.IsNullOrWhiteSpace(body) ? $"ls exited with {rc}" : body;
            Log.Warn($"SCP list failed (rc={rc}): {reason}", "sftp");
            throw new IOException($"SCP 列目录失败 (rc={rc}): {reason}");
        }

        return ScpLsParser.ParseListing(body)
            .Select(e => new FsRow
            {
                Name = e.Name,
                IsDir = e.IsDir,
                IsLink = e.IsLink,
                Size = e.Size,
                Perms = e.Perms,
                HasPerms = e.HasPerms,
            })
            .OrderByDescending(r => r.IsDir)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ==================== Upload / Download ====================

    public bool Exists(string path)
    {
        var raw = _session.ExecAsync(
            $"if [ -e {EscapeArg(path)} ] || [ -L {EscapeArg(path)} ]; then echo __PIXSHELL_EXISTS__; else echo __PIXSHELL_MISSING__; fi; echo __PIXSHELL_RC:$?")
            .GetAwaiter().GetResult();
        var (rc, body) = SftpTransfer.ParseRemoteRC(raw);
        if (rc != 0)
            throw new IOException($"SCP 检查路径失败 (rc={rc}): {body}");
        return body.Contains("__PIXSHELL_EXISTS__", StringComparison.Ordinal);
    }

    public long GetFileSize(string path)
    {
        var name = Path.GetFileName(path);
        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "/";
        return ListDirectory(string.IsNullOrEmpty(parent) ? "/" : parent)
            .FirstOrDefault(x => x.Name == name)?.Size ?? -1;
    }

    public void UploadFile(string localPath, string remotePath)
    {
        using var scp = _session.CreateAndConnectScpClient();
        using var fs = File.OpenRead(localPath);
        scp.Upload(fs, remotePath);
    }

    public void DownloadFile(string remotePath, string localPath)
    {
        using var scp = _session.CreateAndConnectScpClient();
        using var fs = File.Create(localPath);
        scp.Download(remotePath, fs);
    }

    // ==================== Shell operations ====================

    public void CreateEmptyFile(string path)
    {
        const string exists = "__PIXSHELL_FILE_EXISTS__";
        const string created = "__PIXSHELL_FILE_CREATED__";
        const string failed = "__PIXSHELL_FILE_CREATE_FAILED__";
        var escapedPath = EscapeArg(path);
        var output = _session.ExecAsync(
            $"if [ -e {escapedPath} ]; then printf '{exists}'; " +
            $"elif (set -C; : > {escapedPath}) 2>/dev/null; then printf '{created}'; " +
            $"else printf '{failed}'; fi").GetAwaiter().GetResult();

        if (output.Contains(created, StringComparison.Ordinal)) return;
        if (output.Contains(exists, StringComparison.Ordinal))
            throw new IOException("远端已存在同名文件或目录");
        throw new IOException("远端创建文件失败（权限不足或服务器不支持）");
    }

    public void MakeDirectory(string path) => RunShellChecked($"mkdir -p {EscapeArg(path)}", "创建目录");

    public void Delete(string path, bool isDir)
    {
        var cmd = isDir ? $"rm -rf {EscapeArg(path)}" : $"rm -f {EscapeArg(path)}";
        RunShellChecked(cmd, "删除");
    }

    public void Rename(string from, string to) =>
        RunShellChecked($"mv {EscapeArg(from)} {EscapeArg(to)}", "重命名");

    private void RunShellChecked(string command, string action)
    {
        var raw = _session.ExecAsync($"{command} 2>&1; echo __PIXSHELL_RC:$?").GetAwaiter().GetResult();
        var (rc, body) = SftpTransfer.ParseRemoteRC(raw);
        if (rc != 0)
            throw new IOException(string.IsNullOrWhiteSpace(body) ? $"SCP {action}失败 (rc={rc})" : $"SCP {action}失败 (rc={rc}): {body}");
    }

    public void Close()
    {
        _connected = false;
    }

    private static string EscapeArg(string s)
    {
        if (string.IsNullOrEmpty(s)) return "''";
        return $"'{s.Replace("'", "'\\''")}'";
    }
}
