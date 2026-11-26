using System;
using Renci.SshNet;
using Xunit;

namespace PixShell.Tests;

public sealed class SshNetUpgradeTests
{
    [Fact]
    public void SshNetAssemblyIsPatched2026_0_1OrNewer()
    {
        var version = typeof(SshClient).Assembly.GetName().Version;
        Assert.NotNull(version);
        Assert.True(version! >= new Version(2026, 0, 1, 0), $"Unexpected SSH.NET assembly version: {version}");
    }

    [Fact]
    public void ScpShellQuoteNeutralizesShellMetacharactersAndConfiguresScpClientSafely()
    {
        const string malicious = "/tmp/a $(whoami) `id`; echo 'pwn' \"q\"";
        var quoted = RemotePathTransformation.ShellQuote.Transform(malicious);

        Assert.StartsWith("'", quoted);
        Assert.EndsWith("'", quoted);
        // ShellQuote escapes single quotes via "'"'"'"'" (exiting '', double-quoting "'", re-entering '').
        Assert.Contains("'\"'\"'pwn'\"'\"'", quoted);
        Assert.DoesNotContain(malicious, quoted);

        var info = new ConnectionInfo("127.0.0.1", 22, "tester", new PasswordAuthenticationMethod("tester", "pw"));
        using var scp = new ScpClient(info, RemotePathTransformation.ShellQuote);
        Assert.Same(RemotePathTransformation.ShellQuote, scp.RemotePathTransformation);
    }

    [Fact]
    public void ConnectionInfoAndSftpClientConstructCleanlyWithProxyTypesAndModernAlgorithms()
    {
        var info = new ConnectionInfo(
            "127.0.0.1",
            2222,
            "tester",
            ProxyTypes.Socks5,
            "127.0.0.1",
            1080,
            "proxy-user",
            "proxy-pw",
            new PasswordAuthenticationMethod("tester", "pw"),
            new KeyboardInteractiveAuthenticationMethod("tester"));

        Assert.Equal(ProxyTypes.Socks5, info.ProxyType);
        Assert.Contains("curve25519-sha256", info.KeyExchangeAlgorithms.Keys);
        Assert.Contains("chacha20-poly1305@openssh.com", info.Encryptions.Keys);

        using var sftp = new SftpClient(info) { OperationTimeout = TimeSpan.FromSeconds(30) };
        Assert.False(sftp.IsConnected);
    }
}
