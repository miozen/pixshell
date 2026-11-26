using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using PixShell.Store;
using Renci.SshNet.Common;
using Renci.SshNet.Security;
using Xunit;

namespace PixShell.Tests;

public sealed class HostKeyVerificationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _knownHostsPath;

    public HostKeyVerificationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pixshell-knownhosts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _knownHostsPath = Path.Combine(_tempDir, "known_hosts");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
        }
    }

    private static byte[] MakeEd25519Blob(byte seedByte)
    {
        // SSH wire blob: uint32(11) + "ssh-ed25519" + uint32(32) + 32-byte public key
        var name = Encoding.ASCII.GetBytes("ssh-ed25519");
        var pk = Enumerable.Repeat(seedByte, 32).ToArray();
        using var ms = new MemoryStream();
        WriteU32(ms, name.Length);
        ms.Write(name);
        WriteU32(ms, pk.Length);
        ms.Write(pk);
        return ms.ToArray();
    }

    private static void WriteU32(Stream s, int value)
    {
        s.WriteByte((byte)((value >> 24) & 0xFF));
        s.WriteByte((byte)((value >> 16) & 0xFF));
        s.WriteByte((byte)((value >> 8) & 0xFF));
        s.WriteByte((byte)(value & 0xFF));
    }

    private static string MakeHashedToken(string canonicalHost)
    {
        var salt = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
        var hmac = HMACSHA1.HashData(salt, Encoding.UTF8.GetBytes(canonicalHost));
        return $"|1|{Convert.ToBase64String(salt)}|{Convert.ToBase64String(hmac)}";
    }

    [Fact]
    public void FirstConnectRecordsCanonicalHostAndRepeatConnectMatchesAcrossPortAndCase()
    {
        var blob22 = MakeEd25519Blob(0x11);
        var blob2222 = MakeEd25519Blob(0x22);

        var first = KnownHosts.VerifyOrRecord(_knownHostsPath, "App.Example.COM", 22, "ssh-ed25519", blob22);
        Assert.Equal(KnownHosts.HostKeyStatus.NewHostAccepted, first.Status);
        Assert.Equal("app.example.com", first.CanonicalHost);

        var repeat = KnownHosts.VerifyOrRecord(_knownHostsPath, "app.example.com", 22, "ssh-ed25519", blob22);
        Assert.Equal(KnownHosts.HostKeyStatus.Matched, repeat.Status);

        var firstAltPort = KnownHosts.VerifyOrRecord(_knownHostsPath, "App.Example.COM", 2222, "ssh-ed25519", blob2222);
        Assert.Equal(KnownHosts.HostKeyStatus.NewHostAccepted, firstAltPort.Status);
        Assert.Equal("[app.example.com]:2222", firstAltPort.CanonicalHost);

        var repeatAltPort = KnownHosts.VerifyOrRecord(_knownHostsPath, "app.example.com", 2222, "ssh-ed25519", blob2222);
        Assert.Equal(KnownHosts.HostKeyStatus.Matched, repeatAltPort.Status);

        var lines = File.ReadAllLines(_knownHostsPath);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("app.example.com ssh-ed25519 ", lines[0]);
        Assert.StartsWith("[app.example.com]:2222 ssh-ed25519 ", lines[1]);
    }

    [Fact]
    public void SshHostKeyGuardBlocksChangedKeyAndWrapsMismatchWithFingerprints()
    {
        var original = MakeEd25519Blob(0x33);
        var spoofed = MakeEd25519Blob(0x44);

        Assert.Equal(
            KnownHosts.HostKeyStatus.NewHostAccepted,
            KnownHosts.VerifyOrRecord(_knownHostsPath, "prod.internal", 22, "ssh-ed25519", original).Status);

        var beforeText = File.ReadAllText(_knownHostsPath);

        var guard = new SshHostKeyGuard("prod.internal", 22, trustNewHost: true, knownHostsPathOverride: _knownHostsPath);
        var args = new HostKeyEventArgs(new KeyHostAlgorithm("ssh-ed25519", new ED25519Key(new SshKeyData(spoofed))));
        guard.Evaluate(args);

        Assert.False(args.CanTrust);
        Assert.NotNull(guard.LastRejected);
        Assert.Equal(KnownHosts.HostKeyStatus.Mismatch, guard.LastRejected!.Status);

        var wrapped = guard.WrapIfRejected(new SshConnectionException("Key exchange negotiation failed."));
        var hkEx = Assert.IsType<KnownHosts.HostKeyVerificationException>(wrapped);
        Assert.Contains(KnownHosts.ComputeSha256Fingerprint(original), hkEx.Message);
        Assert.Contains(KnownHosts.ComputeSha256Fingerprint(spoofed), hkEx.Message);
        Assert.Equal(beforeText, File.ReadAllText(_knownHostsPath));
    }

    [Fact]
    public void MatchesHashedKnownHostsNegatedPatternsAndBlocksRevokedKeys()
    {
        var good = MakeEd25519Blob(0x55);
        var revoked = MakeEd25519Blob(0x66);
        var hashedToken = MakeHashedToken("[edge.example.net]:2222");

        File.WriteAllText(_knownHostsPath, string.Join('\n', new[]
        {
            $"{hashedToken} ssh-ed25519 {Convert.ToBase64String(good)}",
            $"web.example.net,!blocked.web.example.net ssh-ed25519 {Convert.ToBase64String(good)}",
            $"@revoked * ssh-ed25519 {Convert.ToBase64String(revoked)}",
            ""
        }), new UTF8Encoding(false));

        Assert.Equal(
            KnownHosts.HostKeyStatus.Matched,
            KnownHosts.VerifyOrRecord(_knownHostsPath, "edge.example.net", 2222, "ssh-ed25519", good).Status);

        Assert.Equal(
            KnownHosts.HostKeyStatus.Matched,
            KnownHosts.VerifyOrRecord(_knownHostsPath, "web.example.net", 22, "ssh-ed25519", good).Status);

        Assert.Equal(
            KnownHosts.HostKeyStatus.Revoked,
            KnownHosts.VerifyOrRecord(_knownHostsPath, "new.example.net", 22, "ssh-ed25519", revoked).Status);

        Assert.False(KnownHosts.MatchesHostPattern("*.example.net,!blocked.example.net", "blocked.example.net", 22));
    }

    [Fact]
    public void StrictUnknownModeRejectsUnknownHostWithoutWritingFile()
    {
        var key = MakeEd25519Blob(0x77);
        var res = KnownHosts.VerifyOrRecord(_knownHostsPath, "unknown.local", 22, "ssh-ed25519", key, trustNewHost: false);

        Assert.Equal(KnownHosts.HostKeyStatus.UnknownRejected, res.Status);
        Assert.False(File.Exists(_knownHostsPath));
    }

    [Fact]
    public async Task ConcurrentFirstConnectsRecordAllHostsCleanlyWithoutCorruption()
    {
        var tasks = Enumerable.Range(1, 12).Select(i => Task.Run(() =>
        {
            var blob = MakeEd25519Blob((byte)(0x80 + i));
            var res = KnownHosts.VerifyOrRecord(_knownHostsPath, $"node{i}.cluster.local", 22, "ssh-ed25519", blob);
            Assert.Equal(KnownHosts.HostKeyStatus.NewHostAccepted, res.Status);
        })).ToArray();

        await Task.WhenAll(tasks);

        var lines = File.ReadAllLines(_knownHostsPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        Assert.Equal(12, lines.Length);
    }
}
