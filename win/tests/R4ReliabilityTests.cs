using System;
using System.Threading;
using System.Threading.Tasks;
using PixShell.Sftp;
using PixShell.Store;
using Xunit;

namespace PixShell.Tests;

public sealed class R4ReliabilityTests
{
    [Fact]
    public async Task AsyncTaskObserverReportsFaultedTaskWithoutUnobservedException()
    {
        var tcs = new TaskCompletionSource();
        var errTcs = new TaskCompletionSource<string>();

        AsyncTaskObserver.Observe(tcs.Task, "sftp", "上传", msg => errTcs.TrySetResult(msg));
        tcs.SetException(new InvalidOperationException("remote disconnected"));

        var reported = await errTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Contains("remote disconnected", reported);
    }

    [Fact]
    public async Task AsyncTaskObserverIgnoresCancellationAndHandlesSuccessCleanly()
    {
        var cancelledTcs = new TaskCompletionSource();
        var errorCalled = false;

        AsyncTaskObserver.Observe(cancelledTcs.Task, "sftp", "取消测试", _ => errorCalled = true);
        cancelledTcs.SetCanceled(new CancellationToken(canceled: true));
        await Task.Delay(30);
        Assert.False(errorCalled);

        AsyncTaskObserver.Observe(Task.CompletedTask, "sftp", "正常完成", _ => errorCalled = true);
        await Task.Delay(20);
        Assert.False(errorCalled);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData("dir/sub")]
    [InlineData("dir\\sub")]
    [InlineData("   ")]
    public void RemoteFileNameRejectsTraversalAndDirectorySeparatorsForRenameAndMkdir(string raw)
    {
        Assert.False(RemoteFileName.TryNormalize(raw, out _, out var err));
        Assert.False(string.IsNullOrWhiteSpace(err));
    }

    [Theory]
    [InlineData("  .env  ", ".env")]
    [InlineData("新配置 (1).json", "新配置 (1).json")]
    public void RemoteFileNameNormalizesValidRenameAndMkdirNames(string raw, string expected)
    {
        Assert.True(RemoteFileName.TryNormalize(raw, out var normalized, out var err));
        Assert.Equal(expected, normalized);
        Assert.Equal(string.Empty, err);
    }
}
