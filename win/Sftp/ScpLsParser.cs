using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PixShell.Sftp;

/// <summary>
/// 解析 SCP 后端执行的 <c>LC_ALL=C ls -la</c> 输出（兼容 9 列标准与 8 列 BusyBox/ISO 时间格式，保留空格文件名与符号链接名）。
/// </summary>
public static class ScpLsParser
{
    public readonly record struct ParsedLsEntry(
        string Name,
        bool IsDir,
        bool IsLink,
        long Size,
        uint Perms,
        bool HasPerms);

    // 组 1: 权限位 (10 字符如 drwxr-xr-x / -rwsr-xr-x)
    // 组 2: 大小 (数字或块设备 "major, minor")
    // 时间戳: 支持 ISO (2025-10-24 12:34 / 含秒与时区) 及传统 3 字段 (Oct 24 12:34 / Oct 24  2025)
    // 组 3: 从时间戳后单个空格起直到行尾的完整文件名（含空格与 -> 目标）
    private static readonly Regex LineRegex = new(
        @"^([dlbcps\-][rwxstST\-]{9})[.+@]?\s+\d+\s+\S+\s+\S+\s+(\d+(?:,\s*\d+)?)\s+(?:\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:\s*[+-]\d{4})?|\S+\s+\d{1,2}\s+(?:\d{2}:\d{2}|\d{4})) (.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static List<ParsedLsEntry> ParseListing(string rawListingOutput)
    {
        var entries = new List<ParsedLsEntry>();
        if (string.IsNullOrEmpty(rawListingOutput)) return entries;

        foreach (var rawLine in rawListingOutput.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("total ", StringComparison.Ordinal))
                continue;

            var match = LineRegex.Match(line);
            if (!match.Success) continue;

            var modeStr = match.Groups[1].Value;
            var sizeStr = match.Groups[2].Value;
            var rawName = match.Groups[3].Value;

            var isDir = modeStr[0] == 'd';
            var isLink = modeStr[0] == 'l';
            var name = rawName;
            if (isLink)
            {
                var arrow = rawName.IndexOf(" -> ", StringComparison.Ordinal);
                if (arrow > 0) name = rawName[..arrow];
            }

            if (name is "." or ".." || string.IsNullOrEmpty(name))
                continue;

            long size = 0;
            if (!sizeStr.Contains(','))
                long.TryParse(sizeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out size);

            var perms = ParsePermissionString(modeStr);
            entries.Add(new ParsedLsEntry(name, isDir, isLink, isDir ? 0 : size, perms, HasPerms: true));
        }

        return entries;
    }

    public static uint ParsePermissionString(string modeStr)
    {
        if (string.IsNullOrEmpty(modeStr) || modeStr.Length < 10) return 0;

        uint m = 0;
        // Owner
        if (modeStr[1] == 'r') m |= 0x100;
        if (modeStr[2] == 'w') m |= 0x80;
        if (modeStr[3] is 'x' or 's') m |= 0x40;
        if (modeStr[3] is 's' or 'S') m |= 0x800; // setuid
        // Group
        if (modeStr[4] == 'r') m |= 0x20;
        if (modeStr[5] == 'w') m |= 0x10;
        if (modeStr[6] is 'x' or 's') m |= 0x8;
        if (modeStr[6] is 's' or 'S') m |= 0x400; // setgid
        // Others
        if (modeStr[7] == 'r') m |= 0x4;
        if (modeStr[8] == 'w') m |= 0x2;
        if (modeStr[9] is 'x' or 't') m |= 0x1;
        if (modeStr[9] is 't' or 'T') m |= 0x200; // sticky

        return m;
    }
}
