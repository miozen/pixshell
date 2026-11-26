using System;
using System.Collections.Generic;
using System.Text.Json;
using PixShell.Proxy;
using PixShell.Store;
using Xunit;

namespace PixShell.Tests;

public sealed class ProxyAndSecurityPolicyTests
{
    [Fact]
    public void SshJumpAndMalformedProxyAreRejectedInsteadOfSilentDirectFallback()
    {
        var jump = new ProxyConfig { Id = "p1", Name = "bastion", Type = ProxyType.SshJump, Host = "jump.internal", Port = 22 };
        Assert.Throws<NotSupportedException>(() => ProxyConnectionPolicy.ValidateForSshConnection(jump));

        var missingHost = new ProxyConfig { Id = "p2", Name = "empty-host", Type = ProxyType.Socks5, Host = "   ", Port = 1080 };
        Assert.Throws<ArgumentException>(() => ProxyConnectionPolicy.ValidateForSshConnection(missingHost));

        var badPort = new ProxyConfig { Id = "p3", Name = "bad-port", Type = ProxyType.Http, Host = "proxy.local", Port = 0 };
        Assert.Throws<ArgumentOutOfRangeException>(() => ProxyConnectionPolicy.ValidateForSshConnection(badPort));

        var validSocks5 = new ProxyConfig { Id = "p4", Name = "ok", Type = ProxyType.Socks5, Host = "127.0.0.1", Port = 1080 };
        ProxyConnectionPolicy.ValidateForSshConnection(validSocks5);
        ProxyConnectionPolicy.ValidateForSshConnection(null);

        var defaultProxy = new ProxyConfig();
        Assert.StartsWith("px_", defaultProxy.Id);
        Assert.NotEqual(defaultProxy.Id, new ProxyConfig().Id);
    }

    [Fact]
    public void Fido2OpenSshRejectsActiveProxyRatherThanBypassing()
    {
        var socks = new ProxyConfig { Id = "p1", Name = "corp", Type = ProxyType.Socks5, Host = "10.0.0.2", Port = 1080 };
        Assert.Throws<NotSupportedException>(() => ProxyConnectionPolicy.EnsureOpenSshSupported(socks));
        ProxyConnectionPolicy.EnsureOpenSshSupported(null);
    }

    [Fact]
    public void LegacyNumericProxyType200DeserializesAsSshJumpAndIsBlocked()
    {
        const string json = """[{"id":"legacy","name":"old-jump","type":200,"host":"jump.corp","port":22}]""";
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new ProxyTypeJsonConverter() },
        };
        var list = JsonSerializer.Deserialize<List<ProxyConfig>>(json, opts);
        Assert.NotNull(list);
        Assert.Single(list!);
        Assert.Equal(ProxyType.SshJump, list![0].Type);
        Assert.Throws<NotSupportedException>(() => ProxyConnectionPolicy.ValidateForSshConnection(list[0]));
    }

    [Fact]
    public void WebDavSecurityPolicyAllowsHttpsAndLoopbackHttpButBlocksRemotePlaintextHttp()
    {
        Assert.True(WebDavSecurityPolicy.ValidateEndpoint("https://dav.jianguoyun.com/dav/pixshell/backup.json", out var httpsUri, out var err1));
        Assert.NotNull(httpsUri);
        Assert.Null(err1);

        Assert.True(WebDavSecurityPolicy.ValidateEndpoint("http://127.0.0.1:8080/backup.json", out var loopbackUri, out var err2));
        Assert.NotNull(loopbackUri);
        Assert.Null(err2);

        Assert.False(WebDavSecurityPolicy.ValidateEndpoint("http://192.168.1.50:5005/backup.json", out var lanUri, out var err3));
        Assert.Null(lanUri);
        Assert.False(string.IsNullOrWhiteSpace(err3));

        Assert.False(WebDavSecurityPolicy.ValidateEndpoint("file:///C:/temp/backup.json", out var fileUri, out var err4));
        Assert.Null(fileUri);
        Assert.False(string.IsNullOrWhiteSpace(err4));
    }
}
