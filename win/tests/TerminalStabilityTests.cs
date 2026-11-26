using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PixShell.Highlight;
using PixShell.Terminal;
using Xunit;

namespace PixShell.Tests;

public sealed class TerminalStabilityTests
{
    [Fact]
    public void OutputBatcherCoalescesHighFrequencyFloodIntoSinglePendingDrainSignal()
    {
        var signals = 0;
        TerminalOutputBatcher? batcher = null;
        batcher = new TerminalOutputBatcher(() => Interlocked.Increment(ref signals), flushDelayMs: 50, maxBatchBytes: 64 * 1024, maxPendingBytes: 1024 * 1024);

        for (var i = 0; i < 200; i++)
        {
            batcher.Enqueue(Encoding.UTF8.GetBytes($"line-{i:D4}\n"));
        }

        // Wait once for the coalesced timer to fire; at most 1 pending signal regardless of 200 writes.
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref signals) >= 1, 1000));
        Assert.Equal(1, Volatile.Read(ref signals));

        var batch = batcher.DrainBatch(out var hasMore);
        Assert.False(hasMore);
        var combined = Encoding.UTF8.GetString(batch);
        Assert.Contains("line-0000\n", combined);
        Assert.Contains("line-0199\n", combined);

        batcher.Dispose();
    }

    [Fact]
    public void OutputBatcherDropsOldestChunksUnderBackpressureAndSplitsByMaxBatchBytes()
    {
        var signals = 0;
        using var batcher = new TerminalOutputBatcher(() => Interlocked.Increment(ref signals), flushDelayMs: 10, maxBatchBytes: 1024, maxPendingBytes: 2500);

        for (var i = 0; i < 5; i++)
        {
            batcher.Enqueue(Encoding.UTF8.GetBytes(new string((char)('A' + i), 800)));
        }

        Assert.True(batcher.DroppedChunkCount >= 2);
        Assert.True(Volatile.Read(ref signals) >= 1);

        var first = batcher.DrainBatch(out var hasMore1);
        Assert.True(hasMore1);
        Assert.True(first.Length <= 2048);

        var totalBytes = first.Length;
        while (true)
        {
            var next = batcher.DrainBatch(out var more);
            totalBytes += next.Length;
            if (!more) break;
        }
        Assert.True(totalBytes <= 2500);
    }

    [Fact]
    public async Task SerialWriterNeverBlocksCallerDuringStalledStreamAndPreservesFifoPlusCoalescedResize()
    {
        using var writeGate = new ManualResetEventSlim(false);
        var received = new List<string>();
        var resizes = new List<(uint cols, uint rows)>();
        var allDone = new ManualResetEventSlim(false);

        using var writer = new TerminalSerialWriter(
            bytes =>
            {
                writeGate.Wait(2000);
                lock (received)
                {
                    received.Add(Encoding.UTF8.GetString(bytes));
                    if (received.Count == 3) allDone.Set();
                }
            },
            (cols, rows) =>
            {
                lock (resizes) resizes.Add((cols, rows));
            });

        var sw = Stopwatch.StartNew();
        Assert.True(writer.EnqueueInput(Encoding.UTF8.GetBytes("cmd-1\r")));
        Assert.True(writer.EnqueueInput(Encoding.UTF8.GetBytes("cmd-2\r")));
        Assert.True(writer.EnqueueInput(Encoding.UTF8.GetBytes("cmd-3\r")));
        Assert.True(writer.EnqueueResize(80, 24));
        Assert.True(writer.EnqueueResize(120, 40));
        Assert.True(writer.EnqueueResize(132, 43));
        sw.Stop();

        // Caller returns immediately even while worker is blocked on writeGate.
        Assert.True(sw.ElapsedMilliseconds < 200, $"Caller blocked for {sw.ElapsedMilliseconds}ms");

        writeGate.Set();
        Assert.True(allDone.Wait(2000));
        await Task.Delay(50);

        Assert.Equal(new[] { "cmd-1\r", "cmd-2\r", "cmd-3\r" }, received);
        Assert.Contains((132u, 43u), resizes);
        Assert.True(resizes.Count <= 2);
    }

    [Fact]
    public void SemanticHighlightSkipsOversizedPlainLineAndResetsOnShortEscM()
    {
        bool active = true;
        var shortReset = SemanticHighlight.Decorate("\u001b[mplain error line", dark: true, ref active);
        Assert.False(active);
        Assert.Contains("error", shortReset);

        bool dummy = false;
        var oversized = new string('x', 8000) + " error";
        var sw = Stopwatch.StartNew();
        var outOversized = SemanticHighlight.Decorate(oversized, dark: true, ref dummy);
        sw.Stop();

        Assert.Equal(oversized, outOversized);
        Assert.True(sw.ElapsedMilliseconds < 200, $"Oversized line took {sw.ElapsedMilliseconds}ms");
    }
}
