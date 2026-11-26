using System;
using System.Threading.Tasks;
using PixShell.Logging;

namespace PixShell.Store;

/// <summary>
/// 统一观测 UI 事件触发的无 await 异步任务（R4）：
/// 避免 fire-and-forget 导致异常静默丢失或产生 UnobservedTaskException，同时消除 CS4014。
/// </summary>
public static class AsyncTaskObserver
{
    public static void Observe(
        Task task,
        string category,
        string operationName,
        Action<string>? onError = null)
    {
        if (task == null) return;
        _ = ObserveCoreAsync(task, category, operationName, onError);
    }

    private static async Task ObserveCoreAsync(
        Task task,
        string category,
        string operationName,
        Action<string>? onError)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Error($"{operationName} 失败: {ex.Message}", category);
            try { onError?.Invoke(ex.Message); } catch { }
        }
    }
}
