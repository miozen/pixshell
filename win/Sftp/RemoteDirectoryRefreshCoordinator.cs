using System;
using System.Threading;

namespace PixShell.Sftp;

/// <summary>
/// 控制远端目录异步刷新与交互操作的代次一致性，防止慢请求回填旧目录或跨会话误操作。
/// </summary>
public sealed class RemoteDirectoryRefreshCoordinator
{
    public readonly record struct RefreshTicket(long RefreshGen, int ConnectionGen, string Directory);

    public readonly record struct OperationSnapshot(object? Session, object? RemoteFs, int ConnectionGen, string Directory);

    private readonly object _lock = new();
    private long _refreshGen;
    private int _activeConnectionGen;
    private string _activeDirectory = "/";
    private CancellationTokenSource? _activeCts;

    public string ActiveDirectory
    {
        get
        {
            lock (_lock) return _activeDirectory;
        }
    }

    /// <summary>开启一次新的目录刷新，取消此前未完成的刷新请求。</summary>
    public RefreshTicket Begin(string directory, int connectionGen, out CancellationToken token)
    {
        CancellationTokenSource? previous;
        var next = new CancellationTokenSource();
        long gen;
        lock (_lock)
        {
            previous = _activeCts;
            _activeCts = next;
            _refreshGen++;
            gen = _refreshGen;
            _activeConnectionGen = connectionGen;
            _activeDirectory = string.IsNullOrEmpty(directory) ? "/" : directory;
            token = next.Token;
        }

        if (previous != null)
        {
            try { previous.Cancel(); } catch (ObjectDisposedException) { }
            try { previous.Dispose(); } catch { }
        }

        return new RefreshTicket(gen, connectionGen, string.IsNullOrEmpty(directory) ? "/" : directory);
    }

    /// <summary>在会话切换或 SFTP 断开时使所有在途目录刷新立即失效。</summary>
    public void Invalidate()
    {
        CancellationTokenSource? previous;
        lock (_lock)
        {
            previous = _activeCts;
            _activeCts = null;
            _refreshGen++;
            _activeDirectory = "/";
        }

        if (previous != null)
        {
            try { previous.Cancel(); } catch (ObjectDisposedException) { }
            try { previous.Dispose(); } catch { }
        }
    }

    /// <summary>仅当刷新代次、连接代次与目标目录均仍是当前态且连接存活时，才允许更新 UI 列表。</summary>
    public bool CanApply(RefreshTicket ticket, int currentConnectionGen, bool remoteConnected)
    {
        if (!remoteConnected) return false;
        lock (_lock)
        {
            return _refreshGen == ticket.RefreshGen
                && _activeConnectionGen == ticket.ConnectionGen
                && currentConnectionGen == ticket.ConnectionGen
                && string.Equals(_activeDirectory, ticket.Directory, StringComparison.Ordinal);
        }
    }

    /// <summary>仅当本次失败仍对应当前活动刷新与连接代次时才展示错误提示。</summary>
    public bool CanShowFailure(RefreshTicket ticket, int currentConnectionGen)
    {
        lock (_lock)
        {
            return _refreshGen == ticket.RefreshGen
                && _activeConnectionGen == ticket.ConnectionGen
                && currentConnectionGen == ticket.ConnectionGen;
        }
    }

    /// <summary>在异步列表确认期间核验会话、连接与目录是否仍与操作开始时一致。</summary>
    public static bool CanExecuteOperation(
        OperationSnapshot snapshot,
        object? currentSession,
        object? currentRemoteFs,
        int currentConnectionGen,
        string currentDirectory,
        bool remoteConnected)
    {
        return remoteConnected
            && ReferenceEquals(snapshot.Session, currentSession)
            && ReferenceEquals(snapshot.RemoteFs, currentRemoteFs)
            && snapshot.ConnectionGen == currentConnectionGen
            && string.Equals(snapshot.Directory, currentDirectory, StringComparison.Ordinal);
    }
}
