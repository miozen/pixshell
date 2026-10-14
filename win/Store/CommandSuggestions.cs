using System;
using System.Collections.Generic;
using System.Linq;

namespace PixShell.Store;

/// <summary>命令输入框的本地候选。选择候选只回填文本，发送仍由调用方显式触发。</summary>
public sealed record CommandSuggestion(string Text, string Source, string? Label = null);

/// <summary>合并历史与快捷命令的纯本地建议逻辑，不访问远端、不执行命令。</summary>
public static class CommandSuggestions
{
    public static List<CommandSuggestion> Build(
        string? query,
        IEnumerable<string> history,
        IEnumerable<QuickCommand> quickCommands,
        int limit = 10)
    {
        var needle = (query ?? "").Trim();
        var candidates = new List<(CommandSuggestion item, int score, int order)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var order = 0;

        foreach (var command in history)
        {
            if (string.IsNullOrWhiteSpace(command) || !Matches(command, needle) || !seen.Add(command)) continue;
            candidates.Add((new CommandSuggestion(command, "历史"), Score(command, needle), order++));
        }

        foreach (var quick in quickCommands)
        {
            if (string.IsNullOrWhiteSpace(quick.Command)
                || (!Matches(quick.Name, needle) && !Matches(quick.Command, needle))
                || !seen.Add(quick.Command)) continue;
            var score = Math.Min(Score(quick.Name, needle), Score(quick.Command, needle));
            candidates.Add((new CommandSuggestion(quick.Command, "快捷命令", quick.Name), score, order++));
        }

        return candidates
            .OrderBy(x => x.score)
            .ThenBy(x => x.order)
            .Take(Math.Clamp(limit, 1, 50))
            .Select(x => x.item)
            .ToList();
    }

    private static bool Matches(string? value, string needle) =>
        needle.Length == 0 || (value?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false);

    private static int Score(string? value, string needle)
    {
        if (needle.Length == 0) return 1;
        return value?.StartsWith(needle, StringComparison.OrdinalIgnoreCase) == true ? 0 : 1;
    }
}
