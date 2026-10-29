using System.Text.Json;
using PixShell.Store;
using Xunit;

namespace PixShell.Tests;

public sealed class QuickCommandStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pixshell-tests-" + Guid.NewGuid().ToString("N"));

    public QuickCommandStoreTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public void LoadsLegacyJsonInItsExistingOrder()
    {
        var commands = new[]
        {
            new { Id = "first", Name = "First", Group = "ops", Command = "first" },
            new { Id = "second", Name = "Second", Group = "ops", Command = "second" },
        };
        File.WriteAllText(Path.Combine(_dir, "quick-commands.json"), JsonSerializer.Serialize(commands));

        var store = new QuickCommandStore(_dir);

        Assert.Equal(new[] { "first", "second" }, store.List("ops").Select(c => c.Id));
        Assert.All(store.Commands, command => Assert.True(command.AutoReturn));
    }

    [Fact]
    public void MoveAppendsToTheTargetGroupAndPersists()
    {
        var store = new QuickCommandStore(_dir);
        store.Commands.Clear();
        store.Upsert(new QuickCommand { Id = "one", Name = "One", Group = "source", Command = "echo one" });
        store.Upsert(new QuickCommand { Id = "two", Name = "Two", Group = "source", Command = "echo two" });
        store.Upsert(new QuickCommand { Id = "existing", Name = "Existing", Group = "target", Command = "echo existing" });

        store.Move("one", "target");

        var reloaded = new QuickCommandStore(_dir);
        Assert.Equal("target", reloaded.Commands.Single(c => c.Id == "one").Group);
        Assert.Equal(new[] { "two" }, reloaded.List("source").Select(c => c.Id));
        Assert.Equal(new[] { "existing", "one" }, reloaded.List("target").Select(c => c.Id));
    }

    [Fact]
    public void ReordersOnlyWithinTheSameGroupAndPersists()
    {
        var store = new QuickCommandStore(_dir);
        store.Commands.Clear();
        store.Upsert(new QuickCommand { Id = "first", Name = "First", Group = "ops", Command = "first" });
        store.Upsert(new QuickCommand { Id = "other", Name = "Other", Group = "other", Command = "other" });
        store.Upsert(new QuickCommand { Id = "second", Name = "Second", Group = "ops", Command = "second" });
        store.Upsert(new QuickCommand { Id = "third", Name = "Third", Group = "ops", Command = "third" });

        Assert.False(store.CanMoveUp("first"));
        Assert.True(store.MoveUp("third"));
        Assert.True(store.MoveDown("first"));

        var reloaded = new QuickCommandStore(_dir);
        Assert.Equal(new[] { "third", "first", "second" }, reloaded.List("ops").Select(c => c.Id));
        Assert.Equal(new[] { "other" }, reloaded.List("other").Select(c => c.Id));
        Assert.False(reloaded.MoveDown("second"));
    }

    [Fact]
    public void DragReorderPersistsExplicitSortOrderAndRejectsCrossGroupTargets()
    {
        var store = new QuickCommandStore(_dir);
        store.Commands.Clear();
        store.Upsert(new QuickCommand { Id = "first", Name = "First", Group = "ops", Command = "first" });
        store.Upsert(new QuickCommand { Id = "second", Name = "Second", Group = "ops", Command = "second" });
        store.Upsert(new QuickCommand { Id = "third", Name = "Third", Group = "ops", Command = "third" });
        store.Upsert(new QuickCommand { Id = "other", Name = "Other", Group = "other", Command = "other" });

        Assert.True(store.ReorderBefore("third", "first"));
        Assert.False(store.ReorderBefore("first", "other"));

        var reloaded = new QuickCommandStore(_dir);
        Assert.Equal(new[] { "third", "first", "second" }, reloaded.List("ops").Select(c => c.Id));
        Assert.Equal(new[] { 0, 1, 2 }, reloaded.List("ops").Select(c => c.SortOrder!.Value));
        Assert.Contains("\"sortOrder\"", File.ReadAllText(Path.Combine(_dir, "quick-commands.json")));
    }

    [Fact]
    public void RenderUsesExplicitValuesBeforeDefaults()
    {
        var store = new QuickCommandStore(_dir);
        var command = new QuickCommand
        {
            Command = "tail -n ${lines} ${file}",
            Params = new List<QuickCommandParam>
            {
                new() { Name = "lines", DefaultValue = "100" },
                new() { Name = "file", DefaultValue = "/var/log/app.log" },
            },
        };

        var rendered = store.Render(command, new Dictionary<string, string> { ["lines"] = "20" });

        Assert.Equal("tail -n 20 /var/log/app.log", rendered);
    }

    [Fact]
    public void MergeFromBackupAddsAndUpdatesCommandsWithoutDeletingLocalOnes()
    {
        var store = new QuickCommandStore(_dir);
        store.Commands.Clear();
        store.Upsert(new QuickCommand { Id = "local", Name = "Local", Group = "ops", Command = "echo local" });
        store.Upsert(new QuickCommand { Id = "shared", Name = "Old", Group = "ops", Command = "echo old" });

        var merged = store.MergeFromBackup(new[]
        {
            new QuickCommand
            {
                Id = "shared", Name = "Updated", Group = "ops", Command = "echo updated",
                Params = new List<QuickCommandParam> { new() { Name = "target", DefaultValue = "all" } },
                AutoReturn = false,
            },
            new QuickCommand { Id = "remote", Name = "Remote", Group = "deploy", Command = "deploy", SortOrder = 0 },
        });

        var reloaded = new QuickCommandStore(_dir);
        Assert.Equal(2, merged);
        Assert.Equal(new[] { "local", "shared" }, reloaded.List("ops").Select(c => c.Id));
        Assert.Equal("echo updated", reloaded.Commands.Single(c => c.Id == "shared").Command);
        Assert.False(reloaded.Commands.Single(c => c.Id == "shared").AutoReturn);
        Assert.Equal("all", reloaded.Commands.Single(c => c.Id == "shared").Params!.Single().DefaultValue);
        Assert.Equal(new[] { "remote" }, reloaded.List("deploy").Select(c => c.Id));
    }

    [Fact]
    public void SuggestionsRankPrefixesAndNeverDuplicateCommandText()
    {
        var suggestions = CommandSuggestions.Build(
            "tail",
            new[] { "grep error", "tail -f /var/log/app.log" },
            new[]
            {
                new QuickCommand { Name = "查看日志", Command = "tail -f /var/log/app.log" },
                new QuickCommand { Name = "Tail errors", Command = "tail -n 100 /var/log/error.log" },
            });

        Assert.Equal(new[] { "tail -f /var/log/app.log", "tail -n 100 /var/log/error.log" }, suggestions.Select(x => x.Text));
        Assert.Equal("历史", suggestions[0].Source);
        Assert.Equal("快捷命令", suggestions[1].Source);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch { /* Best-effort cleanup for failed assertions or antivirus locks. */ }
    }
}

public sealed class TransferTasksTests
{
    [Fact]
    public void ReportsProgressAndFinishesAtKnownTotal()
    {
        TransferTasks.Clear();
        var id = TransferTasks.Start(TransferTasks.Direction.Download, "report.txt", "/tmp/report.txt", "C:\\Downloads\\report.txt", 100);

        TransferTasks.Report(id, 40);
        TransferTasks.Report(id, 10); // 进度不能倒退
        TransferTasks.Finish(id, ok: true);

        var task = Assert.Single(TransferTasks.Items);
        Assert.Equal(TransferTasks.State.Done, task.State);
        Assert.Equal(100, task.TransferredBytes);
    }

    [Fact]
    public void FailedTaskCanRequestTheRegisteredRetry()
    {
        TransferTasks.Clear();
        var retries = 0;
        var id = TransferTasks.Start(TransferTasks.Direction.Upload, "report.txt", "C:\\report.txt", "/tmp/report.txt", 10, () => retries++);

        TransferTasks.Finish(id, ok: false, detail: "connection lost");
        TransferTasks.Retry(id);

        var task = Assert.Single(TransferTasks.Items);
        Assert.True(task.CanRetry);
        Assert.Equal(1, retries);
    }
}

public sealed class SftpConnectionEpochTests
{
    [Fact]
    public void CleanupInvalidatesAnInFlightConnectionAttempt()
    {
        var epoch = new PixShell.Sftp.SftpConnectionEpoch();
        var connecting = epoch.Advance();
        var cleanup = epoch.Advance();

        Assert.False(epoch.IsCurrent(connecting));
        Assert.True(epoch.IsCurrent(cleanup));
    }
}

public sealed class ReleaseSelectionTests
{
    [Fact]
    public async Task ReadsSecondPageAndSelectsLegacyWindowsRelease()
    {
        var calls = new List<int>();
        var first = JsonSerializer.Serialize(Enumerable.Range(0, 100).Select(_ => new { tag_name = "macos-v2.0.0", assets = Array.Empty<object>() }));
        const string second = "[{\"tag_name\":\"v0.3.0\",\"assets\":[{\"name\":\"PixShell-0.3.0-win-x64.zip\"}]}]";
        var pages = await PixShell.Store.ReleaseSelection.ReadPages(page => { calls.Add(page); return Task.FromResult(page == 1 ? first : second); });
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(pages));
        Assert.Equal(new[] { 1, 2 }, calls);
        Assert.Equal("0.3.0", PixShell.Store.ReleaseSelection.Choose(doc.RootElement, "windows")!.Value.Version);
    }

    [Fact]
    public void PicksHighestWindowsReleaseAcrossPagesAndIgnoresMacAndDrafts()
    {
        using var doc = JsonDocument.Parse("""
          [
            {"tag_name":"macos-v9.0.0","assets":[{"name":"PixShell-9.0.0-mac-arm64.dmg"}]},
            {"tag_name":"windows-v2.0.0","draft":true,"assets":[{"name":"PixShell-2.0.0-win-x64.zip"}]},
            {"tag_name":"v0.3.0","assets":[{"name":"PixShell-0.3.0-win-x64.zip"}]},
            {"tag_name":"windows-v0.4.0","assets":[{"name":"PixShell-0.4.0-win-x64-setup.exe"}]},
            {"tag_name":"windows-v0.9.0","prerelease":true,"assets":[{"name":"PixShell-0.9.0-win-x64.zip"}]}
          ]
          """);
        var result = PixShell.Store.ReleaseSelection.Choose(doc.RootElement, "windows");
        Assert.NotNull(result);
        Assert.Equal("0.4.0", result.Value.Version);
        Assert.Equal("windows-v0.4.0", result.Value.Release.GetProperty("tag_name").GetString());
    }
}

public sealed class RemoteFileNameTests
{
    [Theory]
    [InlineData("backup")]
    [InlineData("config.json")]
    [InlineData(".env")]
    [InlineData("脚本.sh")]
    public void AllowsAnySingleFileNameFormat(string requested)
    {
        Assert.True(PixShell.Sftp.RemoteFileName.TryNormalize(requested, out var name, out var error));
        Assert.Equal(requested, name);
        Assert.Equal(string.Empty, error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("nested/file.txt")]
    [InlineData("nested\\file.txt")]
    public void RejectsEmptySpecialAndPathNames(string requested)
    {
        Assert.False(PixShell.Sftp.RemoteFileName.TryNormalize(requested, out _, out var error));
        Assert.NotEqual(string.Empty, error);
    }
}
