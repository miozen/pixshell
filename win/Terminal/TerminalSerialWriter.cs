using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace PixShell.Terminal;

/// <summary>
/// 终端输入与 PTY 尺寸同步的后台串行写入器（WIN-BUG-026）：
/// 把 ShellStream.Write / stdin.Write / SendWindowChangeRequest 移出 WPF UI 线程，
/// 在网络抖动或通道背压时不冻结主窗口；同时严格保证按键/粘贴的 FIFO 顺序，并将高频 resize 合并为最新值。
/// </summary>
public sealed class TerminalSerialWriter : IDisposable
{
    private readonly Action<byte[]> _writeBytes;
    private readonly Action<uint, uint> _sendResize;
    private readonly ConcurrentQueue<byte[]> _inputQueue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    private readonly object _resizeLock = new();
    private bool _hasPendingResize;
    private uint _pendingCols;
    private uint _pendingRows;
    private volatile bool _disposed;

    public TerminalSerialWriter(Action<byte[]> writeBytes, Action<uint, uint> sendResize)
    {
        _writeBytes = writeBytes ?? throw new ArgumentNullException(nameof(writeBytes));
        _sendResize = sendResize ?? throw new ArgumentNullException(nameof(sendResize));
        _worker = Task.Run(WorkerLoop);
    }

    public bool EnqueueInput(byte[] bytes)
    {
        if (_disposed || bytes == null || bytes.Length == 0) return false;
        _inputQueue.Enqueue(bytes);
        ReleaseSignal();
        return true;
    }

    public bool EnqueueResize(uint cols, uint rows)
    {
        if (_disposed || cols == 0 || rows == 0) return false;
        lock (_resizeLock)
        {
            _pendingCols = cols;
            _pendingRows = rows;
            _hasPendingResize = true;
        }
        ReleaseSignal();
        return true;
    }

    private void ReleaseSignal()
    {
        if (_disposed) return;
        try { _signal.Release(); }
        catch (ObjectDisposedException) { }
    }

    private async Task WorkerLoop()
    {
        var token = _cts.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                await _signal.WaitAsync(token).ConfigureAwait(false);
                if (token.IsCancellationRequested) break;

                while (!token.IsCancellationRequested && _inputQueue.TryDequeue(out var bytes))
                {
                    try { _writeBytes(bytes); }
                    catch { /* 断开或写入失败时不杀死串行消费循环，交由连接状态收尾 */ }
                }

                uint cols = 0, rows = 0;
                var doResize = false;
                lock (_resizeLock)
                {
                    if (_hasPendingResize)
                    {
                        cols = _pendingCols;
                        rows = _pendingRows;
                        _hasPendingResize = false;
                        doResize = true;
                    }
                }
                if (doResize && !token.IsCancellationRequested)
                {
                    try { _sendResize(cols, rows); }
                    catch { }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _cts.Cancel(); } catch { }
        try { _signal.Release(); } catch { }
        try { _cts.Dispose(); } catch { }
    }
}
