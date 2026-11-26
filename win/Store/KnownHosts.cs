using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PixShell.Logging;
using PixShell.Store;

namespace PixShell;

/// <summary>
/// 主机指纹管理：读写 <c>%USERPROFILE%\.ssh\known_hosts</c>（对齐 mac Store/KnownHosts.swift）。
/// 指纹按 OpenSSH 规则：对 base64 解码后的公钥字节做 SHA256（无 padding base64）/ MD5（冒号 hex）。
/// </summary>
public static class KnownHosts
{
    public enum HostKeyStatus
    {
        Matched,
        NewHostAccepted,
        Mismatch,
        Revoked,
        UnknownRejected,
        InvalidKey,
    }

    public sealed record HostKeyVerifyResult(
        HostKeyStatus Status,
        string CanonicalHost,
        string KeyType,
        string ReceivedSha256,
        string? ExpectedSha256 = null,
        string? Detail = null);

    public sealed class HostKeyVerificationException : Exception
    {
        public HostKeyVerifyResult VerificationResult { get; }

        public HostKeyVerificationException(HostKeyVerifyResult result, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            VerificationResult = result;
        }
    }

    public sealed class Entry
    {
        public string Hosts { get; init; } = "";
        public string KeyType { get; init; } = "";
        public string KeyTypeShort { get; init; } = "";
        public string FingerprintSHA256 { get; init; } = "";
        public string FingerprintMD5 { get; init; } = "";
        public string RawLine { get; init; } = "";
        public string? Marker { get; init; }
        internal byte[] KeyBytes { get; init; } = Array.Empty<byte>();
    }

    public static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "known_hosts");

    public static List<Entry> List()
    {
        var result = new List<Entry>();
        if (!File.Exists(Path)) return result;
        string text;
        try { text = File.ReadAllText(Path); }
        catch { return result; }

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var e = Parse(line);
            if (e != null) result.Add(e);
        }
        return result;
    }

    /// <summary>删除一条指纹：按 RawLine 精确匹配后原子写回。</summary>
    public static void Delete(Entry entry)
    {
        LocalFileTransaction.WithFileLock(Path, () =>
        {
            if (!File.Exists(Path)) return;
            string text;
            try { text = File.ReadAllText(Path); }
            catch (Exception ex)
            {
                Log.Warn("读 known_hosts 失败: " + ex.Message, "known_hosts");
                return;
            }

            var target = entry.RawLine.Trim();
            var kept = text.Split('\n')
                .Where(l => l.Trim() != target)
                .ToList();
            var body = string.Join("\n", kept);
            if (body.Length > 0 && !body.EndsWith("\n", StringComparison.Ordinal))
                body += "\n";

            try
            {
                LocalFileTransaction.WriteAllTextAtomic(Path, body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                Log.Info($"删除主机指纹 {entry.Hosts} ({entry.KeyTypeShort})", "known_hosts");
            }
            catch (Exception ex)
            {
                Log.Warn("写 known_hosts 失败: " + ex.Message, "known_hosts");
            }
        });
    }

    /// <summary>导出 known_hosts 原文到目标文件。</summary>
    public static int Export(string destPath)
    {
        var text = File.Exists(Path) ? File.ReadAllText(Path) : "";
        var dir = System.IO.Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(destPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var count = List().Count;
        Log.Info($"导出主机指纹 {count} 条 → {destPath}", "known_hosts");
        return count;
    }

    public readonly record struct ImportResult(int Added, int SkippedDuplicate, int SkippedInvalid);

    /// <summary>
    /// 从 known_hosts 格式文本合并：跳过空行/注释，按 trim 后整行去重，追加新行。
    /// </summary>
    public static ImportResult ImportMerging(string sourcePath)
    {
        var incoming = File.ReadAllText(sourcePath);
        return ImportMergingText(incoming, System.IO.Path.GetFileName(sourcePath));
    }

    public static ImportResult ImportMergingText(string text, string source = "text")
    {
        return LocalFileTransaction.WithFileLock(Path, () =>
        {
            var existingText = File.Exists(Path) ? File.ReadAllText(Path) : "";
            var existingLines = existingText.Split('\n').ToList();
            var existingKeys = new HashSet<string>(
                existingLines
                    .Select(l => l.Trim())
                    .Where(l => l.Length > 0 && !l.StartsWith('#')));

            var added = 0;
            var skippedDuplicate = 0;
            var skippedInvalid = 0;

            foreach (var raw in text.Split('\n'))
            {
                var trimmed = raw.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
                if (Parse(trimmed) == null)
                {
                    skippedInvalid++;
                    continue;
                }
                if (existingKeys.Contains(trimmed))
                {
                    skippedDuplicate++;
                    continue;
                }
                while (existingLines.Count > 0 && string.IsNullOrWhiteSpace(existingLines[^1]))
                    existingLines.RemoveAt(existingLines.Count - 1);
                existingLines.Add(trimmed);
                existingKeys.Add(trimmed);
                added++;
            }

            if (added > 0)
            {
                var body = string.Join("\n", existingLines);
                if (body.Length > 0 && !body.EndsWith("\n", StringComparison.Ordinal))
                    body += "\n";
                LocalFileTransaction.WriteAllTextAtomic(Path, body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }

            Log.Info($"导入主机指纹 {source}：新增 {added} / 重复 {skippedDuplicate} / 无效 {skippedInvalid}", "known_hosts");
            return new ImportResult(added, skippedDuplicate, skippedInvalid);
        });
    }

    public static string ComputeSha256Fingerprint(byte[] keyData) =>
        "SHA256:" + Base64NoPad(SHA256.HashData(keyData));

    public static string FormatHostToken(string host, int port)
    {
        var h = (host ?? "").Trim();
        if (h.StartsWith('[') && h.EndsWith(']') && h.Length > 2)
            h = h[1..^1];
        h = h.ToLowerInvariant();
        if (port <= 0 || port == 22) return h;
        return $"[{h}]:{port}";
    }

    /// <summary>
    /// 校验服务端主机公钥（遵循 OpenSSH StrictHostKeyChecking=accept-new 语义）：
    /// 1) @revoked 命中直接拒绝；2) 已知主机指纹完全一致放行；
    /// 3) 已知主机指纹不匹配严格拒绝；4) 未知主机在 trustNewHost=true 时原子写入 known_hosts 并放行。
    /// </summary>
    public static HostKeyVerifyResult VerifyOrRecord(
        string host, int port, string keyType, byte[] hostKey, bool trustNewHost = true) =>
        VerifyOrRecord(Path, host, port, keyType, hostKey, trustNewHost);

    public static HostKeyVerifyResult VerifyOrRecord(
        string knownHostsPath, string host, int port, string keyType, byte[] hostKey, bool trustNewHost = true)
    {
        var canonicalHost = FormatHostToken(host, port);
        if (string.IsNullOrWhiteSpace(canonicalHost) || string.IsNullOrWhiteSpace(keyType) || hostKey == null || hostKey.Length == 0)
        {
            return new HostKeyVerifyResult(
                HostKeyStatus.InvalidKey,
                canonicalHost,
                keyType ?? "",
                "",
                Detail: "主机名或服务端公钥数据无效");
        }

        var receivedSha = ComputeSha256Fingerprint(hostKey);
        return LocalFileTransaction.WithFileLock(knownHostsPath, () =>
        {
            var entries = new List<Entry>();
            var existingText = "";
            if (File.Exists(knownHostsPath))
            {
                existingText = File.ReadAllText(knownHostsPath);
                foreach (var raw in existingText.Split('\n'))
                {
                    var trimmed = raw.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
                    var parsed = Parse(trimmed);
                    if (parsed != null) entries.Add(parsed);
                }
            }

            // 1) @revoked 撤销标记优先拦截
            foreach (var entry in entries)
            {
                if (!string.Equals(entry.Marker, "@revoked", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (CryptographicOperations.FixedTimeEquals(entry.KeyBytes, hostKey)
                    && (entry.Hosts == "*" || MatchesHostPattern(entry.Hosts, host, port)))
                {
                    return new HostKeyVerifyResult(
                        HostKeyStatus.Revoked,
                        canonicalHost,
                        keyType,
                        receivedSha,
                        entry.FingerprintSHA256,
                        $"服务端公钥在 {knownHostsPath} 中已被标记为 @revoked ({receivedSha})");
                }
            }

            // 2) 匹配主机条目（忽略 @cert-authority / @revoked）
            var hostEntries = entries
                .Where(e => string.IsNullOrEmpty(e.Marker) && MatchesHostPattern(e.Hosts, host, port))
                .ToList();

            if (hostEntries.Count > 0)
            {
                foreach (var entry in hostEntries)
                {
                    if (string.Equals(entry.KeyType, keyType, StringComparison.Ordinal)
                        && CryptographicOperations.FixedTimeEquals(entry.KeyBytes, hostKey))
                    {
                        return new HostKeyVerifyResult(
                            HostKeyStatus.Matched,
                            canonicalHost,
                            keyType,
                            receivedSha,
                            entry.FingerprintSHA256);
                    }
                }

                var expected = hostEntries.FirstOrDefault(e => string.Equals(e.KeyType, keyType, StringComparison.Ordinal))
                    ?? hostEntries[0];
                return new HostKeyVerifyResult(
                    HostKeyStatus.Mismatch,
                    canonicalHost,
                    keyType,
                    receivedSha,
                    expected.FingerprintSHA256,
                    $"主机 {canonicalHost} 指纹变更！已记录 {expected.FingerprintSHA256}，收到 {receivedSha} ({keyType})");
            }

            // 3) 首次见到该主机
            if (!trustNewHost)
            {
                return new HostKeyVerifyResult(
                    HostKeyStatus.UnknownRejected,
                    canonicalHost,
                    keyType,
                    receivedSha,
                    Detail: $"未在 known_hosts 中找到主机 {canonicalHost} 的信任指纹 ({receivedSha})");
            }

            var line = $"{canonicalHost} {keyType} {Convert.ToBase64String(hostKey)}";
            var sb = new StringBuilder(existingText.Replace("\r\n", "\n"));
            if (sb.Length > 0 && sb[^1] != '\n') sb.Append('\n');
            sb.Append(line).Append('\n');
            LocalFileTransaction.WriteAllTextAtomic(knownHostsPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            return new HostKeyVerifyResult(
                HostKeyStatus.NewHostAccepted,
                canonicalHost,
                keyType,
                receivedSha,
                receivedSha);
        });
    }

    internal static bool MatchesHostPattern(string hostField, string host, int port)
    {
        if (string.IsNullOrWhiteSpace(hostField)) return false;
        var canonical = FormatHostToken(host, port);
        if (string.IsNullOrEmpty(canonical)) return false;

        var positiveMatch = false;
        foreach (var rawToken in hostField.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var token = rawToken.Trim();
            if (token.Length == 0) continue;

            var negated = token[0] == '!';
            var pattern = negated ? token[1..] : token;
            if (pattern.Length == 0) continue;

            if (MatchSingleToken(pattern, canonical, port))
            {
                if (negated) return false;
                positiveMatch = true;
            }
        }
        return positiveMatch;
    }

    private static bool MatchSingleToken(string pattern, string canonicalHost, int port)
    {
        if (pattern.StartsWith("|1|", StringComparison.Ordinal))
        {
            var parts = pattern.Split('|');
            if (parts.Length != 4) return false;
            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expectedHash = Convert.FromBase64String(parts[3]);
                var actualHash = HMACSHA1.HashData(salt, Encoding.UTF8.GetBytes(canonicalHost));
                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            catch
            {
                return false;
            }
        }

        if (string.Equals(pattern, canonicalHost, StringComparison.OrdinalIgnoreCase))
            return true;

        // 兼容 22 端口带 [host]:22 写法
        if (port == 22 && string.Equals(pattern, $"[{canonicalHost}]:22", StringComparison.OrdinalIgnoreCase))
            return true;

        if (pattern.IndexOfAny(new[] { '*', '?' }) >= 0)
            return WildcardMatches(pattern, canonicalHost);

        return false;
    }

    private static bool WildcardMatches(string pattern, string text)
    {
        var pi = 0;
        var ti = 0;
        var starIdx = -1;
        var matchIdx = 0;

        while (ti < text.Length)
        {
            if (pi < pattern.Length && (pattern[pi] == '?' || char.ToLowerInvariant(pattern[pi]) == char.ToLowerInvariant(text[ti])))
            {
                pi++;
                ti++;
            }
            else if (pi < pattern.Length && pattern[pi] == '*')
            {
                starIdx = pi++;
                matchIdx = ti;
            }
            else if (starIdx != -1)
            {
                pi = starIdx + 1;
                ti = ++matchIdx;
            }
            else
            {
                return false;
            }
        }

        while (pi < pattern.Length && pattern[pi] == '*') pi++;
        return pi == pattern.Length;
    }

    private static Entry? Parse(string line)
    {
        var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (tokens.Count < 3) return null;

        string? marker = null;
        if (tokens[0].StartsWith('@'))
        {
            marker = tokens[0];
            tokens.RemoveAt(0);
            if (tokens.Count < 3) return null;
        }

        var hosts = tokens[0];
        var keyType = tokens[1];
        var b64 = tokens[2];
        byte[] keyData;
        try { keyData = Convert.FromBase64String(b64); }
        catch { return null; }

        return new Entry
        {
            Hosts = hosts,
            KeyType = keyType,
            KeyTypeShort = ShortType(keyType),
            FingerprintSHA256 = "SHA256:" + Base64NoPad(SHA256.HashData(keyData)),
            FingerprintMD5 = "MD5:" + Md5Colon(MD5.HashData(keyData)),
            RawLine = line,
            Marker = marker,
            KeyBytes = keyData,
        };
    }

    private static string ShortType(string t)
    {
        var lower = t.ToLowerInvariant();
        if (lower.Contains("ed25519")) return "ED25519";
        if (lower.Contains("ecdsa")) return "ECDSA";
        if (lower.Contains("rsa")) return "RSA";
        if (lower.Contains("dss") || lower.Contains("dsa")) return "DSA";
        return t.ToUpperInvariant();
    }

    private static string Base64NoPad(byte[] hash) =>
        Convert.ToBase64String(hash).TrimEnd('=');

    private static string Md5Colon(byte[] hash) =>
        string.Join(":", hash.Select(b => b.ToString("x2")));
}
