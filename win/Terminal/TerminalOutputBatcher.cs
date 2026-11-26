using System;
using System.Collections.Generic;
using System.Threading;

namespace PixShell.Terminal;

/// <summary>
/// 终端输出合批与背压调度器（WIN-BUG-025/026）：
/// 1) 后台读线程以 O(1) 入队并合并 16ms 窗口内的零碎块，避免每次 read(4KB) 都触发 Dispatcher.BeginInvoke 风暴；
/// 2) 单会话同一时刻至多存在 1 次未消费的 UI flush 信号，防止输出洪峰堵死 WPF UI 线程；
/// 3) 队列总字节设有上限（默认 2 MiB），极端背压时丢弃最旧待渲染块以保住客户端内存与交互响应。
/// </summary>
public sealed class TerminalOutputBatcher : IDisposable
{
    private readonly Action _notifyDrainReady;
    private readonly int _flushDelayMs;
    private readonly int _maxBatchBytes;
    private readonly int _maxPendingBytes;

    private readonly object _lock = new();
    private readonly Queue<byte[]> _chunks = new();
    private readonly Timer _timer;

    private int _queuedBytes;
    private bool _timerArmed;
    private bool _drainSignaled;
    private bool _disposed;

    public int DroppedChunkCount { get; private set; }

    public TerminalOutputBatcher(
        Action notifyDrainReady,
        int flushDelayMs = 16,
        int maxBatchBytes = 64 * 1024,
        int maxPendingBytes = 2 * 1024 * 1024)
    {
        _notifyDrainReady = notifyDrainReady ?? throw new ArgumentNullException(nameof(notifyDrainReady));
        _flushDelayMs = Math.Max(0, flushDelayMs);
        _maxBatchBytes = Math.Max(1024, maxBatchBytes);
        _maxPendingBytes = Math.Max(_maxBatchBytes, maxPendingBytes);
        _timer = new Timer(_ => SignalDrain(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Enqueue(byte[] utf8Bytes)
    {
        if (utf8Bytes == null || utf8Bytes.Length == 0) return;

        var signalNow = false;
        lock (_lock)
        {
            if (_disposed) return;

            while (_chunks.Count > 0 && _queuedBytes + utf8Bytes.Length > _maxPendingBytes)
            {
                var dropped = _chunks.Dequeue();
                _queuedBytes -= dropped.Length;
                DroppedChunkCount++;
            }

            _chunks.Enqueue(utf8Bytes);
            _queuedBytes += utf8Bytes.Length;

            if (_drainSignaled) return;

            if (_queuedBytes >= _maxBatchBytes || _flushDelayMs == 0)
            {
                _timerArmed = false;
                _drainSignaled = true;
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
                signalNow = true;
            }
            else if (!_timerArmed)
            {
                _timerArmed = true;
                _timer.Change(_flushDelayMs, Timeout.Infinite);
            }
        }

        if (signalNow)
            _notifyDrainReady();
    }

    private void SignalDrain()
    {
        lock (_lock)
        {
            if (_disposed || _drainSignaled || _chunks.Count == 0)
            {
                _timerArmed = false;
                return;
            }
            _timerArmed = false;
            _drainSignaled = true;
        }
        _notifyDrainReady();
    }

    /// <summary>
    /// 从当前待消费队列提取不超过 <c>maxBatchBytes</c> 的合批字节；
    /// 若仍有剩余数据，自动安排下一轮延迟信号并返回 <paramref name="hasMore"/>=true。
    /// </summary>
    public byte[] DrainBatch(out bool hasMore)
    {
        byte[] result;
        var rearmDelay = false;

        lock (_lock)
        {
            if (_disposed || _chunks.Count == 0)
            {
                _drainSignaled = false;
                _timerArmed = false;
                hasMore = false;
                return Array.Empty<byte>();
            }

            var total = 0;
            var picked = new List<byte[]>();
            while (_chunks.Count > 0)
            {
                var head = _chunks.Peek();
                if (picked.Count > 0 && total + head.Length > _maxBatchBytes)
                    break;
                _chunks.Dequeue();
                _queuedBytes -= head.Length;
                total += head.Length;
                picked.Add(head);
                if (total >= _maxBatchBytes)
                    break;
            }

            if (picked.Count == 1)
            {
                result = picked[0];
            }
            else
            {
                result = new byte[total];
                var offset = 0;
                foreach (var c in picked)
                {
                    Buffer.BlockCopy(c, 0, result, offset, c.Length);
                    offset += c.Length;
                }
            }

            hasMore = _chunks.Count > 0;
            _drainSignaled = false;
            if (hasMore && !_timerArmed)
            {
                _timerArmed = true;
                rearmDelay = true;
            }
        }

        if (rearmDelay)
            _timer.Change(_flushDelayMs == 0 ? 1 : _flushDelayMs, Timeout.Infinite);

        return result;
    }

    public void Reset()
    {
        lock (_lock)
        {
            _chunks.Clear();
            _queuedBytes = 0;
            _timerArmed = false;
            _drainSignaled = false;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _chunks.Clear();
            _queuedBytes = 0;
            _timerArmed = false;
            _drainSignaled = false;
        }
        _timer.Dispose();
    }
}
