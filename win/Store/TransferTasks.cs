using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PixShell;

/// <summary>
/// SFTP 传输任务的进程内状态。任务本身不持久化：连接断开或应用退出后不能假装可恢复，
/// W2b 将在此模型上增加单文件断点续传。
/// </summary>
public static class TransferTasks
{
    public enum Direction { Download, Upload }
    public enum State { Running, Done, Failed, Cancelled }

    public sealed class Task
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public Direction Direction { get; init; }
        public string Name { get; init; } = "";
        public string Source { get; init; } = "";
        public string Dest { get; init; } = "";
        public long TotalBytes { get; init; }
        public long TransferredBytes { get; internal set; }
        public State State { get; internal set; } = State.Running;
        public string Detail { get; internal set; } = "";
        public DateTime Started { get; init; } = DateTime.Now;
        internal Action? Retry { get; init; }
        internal CancellationTokenSource? Cancellation { get; init; }
        public bool CanRetry => (State == State.Failed || State == State.Cancelled) && Retry != null;
        public bool CanCancel => State == State.Running && Cancellation != null;
    }

    private static readonly List<Task> _tasks = new();
    private const int MaxKeep = 30;
    private static SynchronizationContext? _notificationContext;

    public static event Action? Changed;

    /// <summary>由 WPF UI 在其线程上调用；纯逻辑测试和无 UI 场景无需调用。</summary>
    public static void CaptureNotificationContext() => _notificationContext ??= SynchronizationContext.Current;

    public static IReadOnlyList<Task> Items
    {
        get { lock (_tasks) return _tasks.ToList(); }
    }

    public static Guid Start(Direction direction, string name, string source, string dest, long totalBytes, Action? retry = null, CancellationTokenSource? cancellation = null)
    {
        var task = new Task
        {
            Direction = direction,
            Name = name,
            Source = source,
            Dest = dest,
            TotalBytes = Math.Max(0, totalBytes),
            Retry = retry,
            Cancellation = cancellation,
        };
        lock (_tasks)
        {
            _tasks.Insert(0, task);
            if (_tasks.Count > MaxKeep) _tasks.RemoveRange(MaxKeep, _tasks.Count - MaxKeep);
        }
        Notify();
        return task.Id;
    }

    public static void Report(Guid id, long transferredBytes)
    {
        lock (_tasks)
        {
            var task = _tasks.FirstOrDefault(x => x.Id == id);
            if (task == null || task.State != State.Running) return;
            task.TransferredBytes = Math.Max(task.TransferredBytes, Math.Max(0, transferredBytes));
        }
        Notify();
    }

    public static void Finish(Guid id, bool ok, string detail = "")
    {
        lock (_tasks)
        {
            var task = _tasks.FirstOrDefault(x => x.Id == id);
            if (task == null) return;
            task.State = ok ? State.Done : State.Failed;
            task.Detail = detail;
            if (ok && task.TotalBytes > 0) task.TransferredBytes = task.TotalBytes;
        }
        Notify();
    }

    public static void Cancel(Guid id)
    {
        CancellationTokenSource? cancellation;
        lock (_tasks) cancellation = _tasks.FirstOrDefault(x => x.Id == id)?.Cancellation;
        cancellation?.Cancel();
    }

    public static void FinishCancelled(Guid id, string detail = "已取消；可继续或重新开始")
    {
        lock (_tasks)
        {
            var task = _tasks.FirstOrDefault(x => x.Id == id);
            if (task == null) return;
            task.State = State.Cancelled;
            task.Detail = detail;
        }
        Notify();
    }

    public static void Retry(Guid id)
    {
        Action? action;
        lock (_tasks) action = _tasks.FirstOrDefault(x => x.Id == id)?.Retry;
        action?.Invoke();
    }

    public static void Clear()
    {
        lock (_tasks) _tasks.Clear();
        Notify();
    }

    private static void Notify()
    {
        var context = _notificationContext;
        if (context == null || SynchronizationContext.Current == context) Changed?.Invoke();
        else context.Post(_ => Changed?.Invoke(), null);
    }
}
