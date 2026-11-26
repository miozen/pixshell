using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PixShell.Proxy;

/// <summary>
/// 代理类型。字段/归一化规则对齐老仓库 packages/proxy/src/index.js（createProxy/normalizeProxyType），
/// 与 mac 版 Proxy/ProxyConfig.swift 的 ProxyType 1:1 对应。
///
/// 关于 SshJump（跳板机）：为保留老配置反序列化兼容性保留该枚举，
/// 但在连接阶段严格拒绝（禁止静默回退为直连，WIN-BUG-013A）。
/// </summary>
public enum ProxyType
{
    Socks5,
    Socks4,
    Http,
    SshJump,
}

/// <summary>
/// 代理配置模型。默认值/端口规则与 JS createProxy/defaultPort、mac ProxyConfig 1:1 对齐。
/// </summary>
public class ProxyConfig
{
    public string Id { get; set; } = NewId();
    public string Name { get; set; } = "proxy";
    public ProxyType Type { get; set; } = ProxyType.Socks5;
    public string Host { get; set; } = "";
    public int Port { get; set; } = DefaultPort(ProxyType.Socks5);
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>对齐 JS `'px_' + Date.now().toString(36)`；.NET Convert.ToString 不支持 36 进制，手工编码并附短随机后缀防毫秒碰撞。</summary>
    public static string NewId()
    {
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        var v = Math.Max(0L, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        Span<char> buf = stackalloc char[16];
        var pos = buf.Length;
        do
        {
            buf[--pos] = alphabet[(int)(v % 36)];
            v /= 36;
        } while (v > 0 && pos > 0);
        return "px_" + new string(buf[pos..]) + "_" + Guid.NewGuid().ToString("N")[..4];
    }

    /// <summary>对齐 JS defaultPort / mac ProxyConfig.defaultPort。</summary>
    public static int DefaultPort(ProxyType type) => type switch
    {
        ProxyType.Http => 8080,
        ProxyType.SshJump => 22,
        _ => 1080, // socks4/socks5
    };

    /// <summary>对齐 JS normalizeProxyType：历史数字码 100→socks5、200→ssh-jump，合法字符串原样识别，其余回落 socks5。</summary>
    public static ProxyType NormalizeType(string raw) => raw switch
    {
        "100" => ProxyType.Socks5,
        "200" => ProxyType.SshJump,
        "socks4" => ProxyType.Socks4,
        "socks5" => ProxyType.Socks5,
        "http" => ProxyType.Http,
        "ssh-jump" => ProxyType.SshJump,
        _ => ProxyType.Socks5,
    };

    public string DisplayName => Type switch
    {
        ProxyType.Socks5 => "SOCKS5",
        ProxyType.Socks4 => "SOCKS4",
        ProxyType.Http => "HTTP",
        ProxyType.SshJump => "SSH跳板(暂不支持)",
        _ => Type.ToString(),
    };
}

/// <summary>
/// 代理拨号安全策略（WIN-BUG-013A）：
/// 配置了代理时严禁任何静默直连降级（SshJump、空主机/非法端口、或不支持代理的外部 OpenSSH 路径一律显式拒绝）。
/// </summary>
public static class ProxyConnectionPolicy
{
    public static void ValidateForSshConnection(ProxyConfig? proxy)
    {
        if (proxy == null) return;

        var name = string.IsNullOrWhiteSpace(proxy.Name) ? proxy.Id : proxy.Name;
        if (proxy.Type == ProxyType.SshJump)
        {
            throw new NotSupportedException(
                $"代理「{name}」类型为 ssh-jump（跳板机），当前版本暂不支持 SSH 跳板机连接，已拒绝静默直连。" +
                "请改用 SOCKS5/SOCKS4/HTTP 代理，或将该主机的代理设为“无（直连）”。");
        }

        if (proxy.Type is not (ProxyType.Socks5 or ProxyType.Socks4 or ProxyType.Http))
        {
            throw new NotSupportedException($"代理「{name}」类型（{proxy.Type}）不受支持，已拒绝静默直连。");
        }

        if (string.IsNullOrWhiteSpace(proxy.Host))
        {
            throw new ArgumentException($"代理「{name}」缺少主机地址，已拒绝静默直连。");
        }

        if (proxy.Port is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(proxy), $"代理「{name}」端口无效（{proxy.Port}），已拒绝静默直连。");
        }
    }

    public static void EnsureOpenSshSupported(ProxyConfig? proxy)
    {
        if (proxy == null) return;
        ValidateForSshConnection(proxy);
        var name = string.IsNullOrWhiteSpace(proxy.Name) ? proxy.Id : proxy.Name;
        throw new NotSupportedException(
            $"FIDO2 硬件密钥会话走系统 OpenSSH 客户端，当前暂不支持经代理「{name}」({proxy.DisplayName}) 连接，已拒绝静默直连。");
    }
}

/// <summary>type 字段可能是字符串也可能是数字（老历史数字码），两种都能读；写回时总是写字符串。</summary>
internal sealed class ProxyTypeJsonConverter : JsonConverter<ProxyType>
{
    public override ProxyType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return ProxyConfig.NormalizeType(reader.GetInt32().ToString());
        var s = reader.GetString() ?? "";
        return ProxyConfig.NormalizeType(s);
    }

    public override void Write(Utf8JsonWriter writer, ProxyType value, JsonSerializerOptions options)
    {
        var s = value switch
        {
            ProxyType.Socks5 => "socks5",
            ProxyType.Socks4 => "socks4",
            ProxyType.Http => "http",
            ProxyType.SshJump => "ssh-jump",
            _ => "socks5",
        };
        writer.WriteStringValue(s);
    }
}
