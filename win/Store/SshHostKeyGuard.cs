using System;
using PixShell.Logging;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace PixShell.Store;

/// <summary>
/// 为 SSH.NET 客户端（SshClient / SftpClient / ScpClient）统一挂载主机公钥校验钩子，
/// 防止未校验 HostKey 导致静默接受伪造或变更后的服务端公钥（WIN-BUG-012A）。
/// </summary>
internal sealed class SshHostKeyGuard
{
    private readonly string _logicalHost;
    private readonly int _port;
    private readonly bool _trustNewHost;
    private readonly string? _knownHostsPathOverride;

    public KnownHosts.HostKeyVerifyResult? LastRejected { get; private set; }

    public SshHostKeyGuard(string logicalHost, int port, bool trustNewHost = true, string? knownHostsPathOverride = null)
    {
        _logicalHost = logicalHost;
        _port = port;
        _trustNewHost = trustNewHost;
        _knownHostsPathOverride = knownHostsPathOverride;
    }

    public void Attach(BaseClient client)
    {
        client.HostKeyReceived += OnHostKeyReceived;
    }

    public void Connect(BaseClient client)
    {
        Attach(client);
        try
        {
            client.Connect();
        }
        catch (Exception ex)
        {
            throw WrapIfRejected(ex);
        }
    }

    internal void Evaluate(HostKeyEventArgs e) => OnHostKeyReceived(this, e);

    private void OnHostKeyReceived(object? sender, HostKeyEventArgs e)
    {
        var path = _knownHostsPathOverride ?? KnownHosts.Path;
        var res = KnownHosts.VerifyOrRecord(path, _logicalHost, _port, e.HostKeyName, e.HostKey, _trustNewHost);
        if (res.Status is KnownHosts.HostKeyStatus.Matched or KnownHosts.HostKeyStatus.NewHostAccepted)
        {
            e.CanTrust = true;
            if (res.Status == KnownHosts.HostKeyStatus.NewHostAccepted)
                Log.Info($"首次信任并记录主机指纹 {res.CanonicalHost} ({res.KeyType}) {res.ReceivedSha256}", "known_hosts");
            return;
        }

        e.CanTrust = false;
        LastRejected = res;
        Log.Error($"拒绝主机指纹验证 {res.CanonicalHost}: {res.Detail}", "known_hosts");
    }

    public Exception WrapIfRejected(Exception ex)
    {
        if (LastRejected == null) return ex;
        var r = LastRejected;
        var detail = r.Status switch
        {
            KnownHosts.HostKeyStatus.Mismatch =>
                $"主机 {r.CanonicalHost} 的公钥指纹已变化，连接已出于安全原因拦截！\n" +
                $"已记录指纹: {r.ExpectedSha256}\n" +
                $"当前服务端: {r.ReceivedSha256} ({r.KeyType})\n\n" +
                "若确认服务端已合法重装或更换密钥，请在「文件 → 主机指纹管理…」删除旧记录后重连。",
            KnownHosts.HostKeyStatus.Revoked =>
                $"主机 {r.CanonicalHost} 提供的公钥指纹已被标记为 @revoked 撤销，拒绝连接。\n当前服务端: {r.ReceivedSha256}",
            _ => r.Detail ?? $"主机 {r.CanonicalHost} 公钥验证未通过 ({r.Status})。",
        };
        return new KnownHosts.HostKeyVerificationException(r, detail, ex);
    }
}
