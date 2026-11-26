using System.Text.Json;
using PixShell.Sftp;
using PixShell.Store;
using Xunit;

namespace PixShell.Tests;

public sealed class LocalTransactionAndTargetTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pixshell-r1-3-" + Guid.NewGuid().ToString("N"));

    public LocalTransactionAndTargetTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }

    // ==================== WIN-BUG-011A ====================

    [Fact]
    public void QuickCommandStorePreservesIntentionalEmptyArrayAndBacksUpCorruptFile()
    {
        var path = Path.Combine(_dir, "quick-commands.json");
        File.WriteAllText(path, "[]");

        var emptyStore = new QuickCommandStore(_dir);
        Assert.Empty(emptyStore.Commands);
        Assert.Equal("[]", File.ReadAllText(path).Trim());

        var corruptDir = Path.Combine(_dir, "corrupt");
        Directory.CreateDirectory(corruptDir);
        var corruptPath = Path.Combine(corruptDir, "quick-commands.json");
        const string brokenContent = "{broken-json";
        File.WriteAllText(corruptPath, brokenContent);

        var recovered = new QuickCommandStore(corruptDir);
        Assert.NotEmpty(recovered.Commands);
        Assert.Equal(brokenContent, File.ReadAllText(corruptPath));
        Assert.NotEmpty(Directory.GetFiles(corruptDir, "quick-commands.json.corrupt.*"));
    }

    // ==================== WIN-BUG-011B ====================

    [Fact]
    public void MutateJsonStrictRefusesToOverwriteCorruptStoreAndCreatesBackup()
    {
        var credsPath = Path.Combine(_dir, "creds.dat");
        const string corruptPayload = "{ \"host-1\": invalid-json ";
        File.WriteAllText(credsPath, corruptPayload);

        Assert.Throws<InvalidDataException>(() =>
            LocalFileTransaction.MutateJsonStrict<Dictionary<string, string>>(
                credsPath,
                () => new Dictionary<string, string>(),
                map => map["host-2"] = "secret"));

        Assert.Equal(corruptPayload, File.ReadAllText(credsPath));
        Assert.NotEmpty(Directory.GetFiles(_dir, "creds.dat.corrupt.*"));
    }

    [Fact]
    public async Task MutateJsonStrictPreservesAllConcurrentReadModifyWriteUpdates()
    {
        var storePath = Path.Combine(_dir, "concurrent.json");
        const int workers = 8;
        const int perWorker = 10;

        var tasks = Enumerable.Range(0, workers).Select(w => Task.Run(() =>
        {
            for (var i = 0; i < perWorker; i++)
            {
                var key = $"w{w}-k{i}";
                LocalFileTransaction.MutateJsonStrict<Dictionary<string, string>>(
                    storePath,
                    () => new Dictionary<string, string>(),
                    map => map[key] = "ok");
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        var finalMap = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(storePath))!;
        Assert.Equal(workers * perWorker, finalMap.Count);
    }

    // ==================== WIN-BUG-015B ====================

    [Fact]
    public void MoveEntryWithCrossVolumeFallbackCopiesNestedDirectoriesAndRemovesSource()
    {
        var srcDir = Path.Combine(_dir, "staging", "bundle");
        var subDir = Path.Combine(srcDir, "nested");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(srcDir, "root.txt"), "root-data");
        File.WriteAllText(Path.Combine(subDir, "leaf.bin"), "leaf-data");

        var dstDir = Path.Combine(_dir, "dest", "bundle");
        SftpTransfer.MoveEntryWithCrossVolumeFallback(srcDir, dstDir, forceCopyFallback: true);

        Assert.False(Directory.Exists(srcDir));
        Assert.Equal("root-data", File.ReadAllText(Path.Combine(dstDir, "root.txt")));
        Assert.Equal("leaf-data", File.ReadAllText(Path.Combine(dstDir, "nested", "leaf.bin")));
    }

    // ==================== WIN-BUG-019A ====================

    [Fact]
    public void CommandTargetResolverTracksStableSessionIdAndBlocksDisconnectedTarget()
    {
        var afterPredecessorClosed = new List<SessionTargetItem>
        {
            new("id-b", "Server B", Connected: true),
            new("id-c", "Server C", Connected: true),
        };

        var resolvedB = CommandTargetResolver.ResolveTargetSessionIds(
            SendTarget.Session("id-b", "Server B"),
            afterPredecessorClosed,
            activeSessionId: "id-c");
        Assert.Equal(new[] { "id-b" }, resolvedB);

        var whenBClosed = new List<SessionTargetItem>
        {
            new("id-c", "Server C", Connected: true),
        };
        var resolvedMissing = CommandTargetResolver.ResolveTargetSessionIds(
            SendTarget.Session("id-b", "Server B"),
            whenBClosed,
            activeSessionId: "id-c");
        Assert.Empty(resolvedMissing);

        var unavailable = CommandTargetResolver.ResolveTargetSessionIds(
            SendTarget.Unavailable("id-b", "Server B（已断开）"),
            whenBClosed,
            activeSessionId: "id-c");
        Assert.Empty(unavailable);
    }
}
