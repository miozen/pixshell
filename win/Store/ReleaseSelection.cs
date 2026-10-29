using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PixShell.Store;

public static class ReleaseSelection
{
    public static async Task<List<JsonElement>> ReadPages(Func<int, Task<string>> fetch)
    {
        var releases = new List<JsonElement>();
        for (var page = 1; ; page++)
        {
            using var doc = JsonDocument.Parse(await fetch(page));
            if (doc.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("Release 列表格式无效");
            foreach (var item in doc.RootElement.EnumerateArray()) releases.Add(item.Clone());
            if (doc.RootElement.GetArrayLength() < 100) return releases;
        }
    }

    public static (string Version, JsonElement Release)? Choose(JsonElement pages, string platform)
    {
        (Version version, JsonElement release)? best = null;
        foreach (var release in pages.EnumerateArray())
        {
            if (release.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True ||
                release.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True) continue;
            if (!release.TryGetProperty("tag_name", out var tagValue)) continue;
            var tag = tagValue.GetString() ?? "";
            var match = Regex.Match(tag, @"^(?:(windows|macos)-)?v(\d+)\.(\d+)\.(\d+)$", RegexOptions.CultureInvariant);
            if (!match.Success || (match.Groups[1].Success && match.Groups[1].Value != platform)) continue;
            if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) continue;
            bool hasAsset = assets.EnumerateArray().Any(a => a.TryGetProperty("name", out var n) &&
                (n.GetString() ?? "").Contains(platform == "windows" ? "win-x64" : "mac-", StringComparison.OrdinalIgnoreCase));
            if (!hasAsset || !Version.TryParse($"{match.Groups[2]}.{match.Groups[3]}.{match.Groups[4]}", out var v)) continue;
            if (best == null || v > best.Value.version) best = (v, release.Clone());
        }
        return best is null ? null : (best.Value.version.ToString(), best.Value.release);
    }
}
