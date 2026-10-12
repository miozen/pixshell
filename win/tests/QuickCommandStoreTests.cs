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
        Assert.Equal(new[] { 0, 1, 2 }, reloaded.List("ops").Select(c => c.SortOrder));
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

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch { /* Best-effort cleanup for failed assertions or antivirus locks. */ }
    }
}
