using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using PixShell.Sftp;
using Xunit;

namespace PixShell.Tests;

public sealed class LocalArchiveIntegrationTests : IDisposable
{
    private readonly string _root;

    public LocalArchiveIntegrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "pixshell-itest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task PackAndExtractLocalArchiveWithRealTarPreservesTreeDotfilesSpacesAndMovesCleanly()
    {
        var srcDir = Path.Combine(_root, "src");
        var subDir = Path.Combine(srcDir, "sub dir");
        var stageDir = Path.Combine(_root, "stage");
        var destDir = Path.Combine(_root, "dest");
        Directory.CreateDirectory(subDir);
        Directory.CreateDirectory(stageDir);
        Directory.CreateDirectory(destDir);

        var nestedFile = Path.Combine(subDir, "hello world.txt");
        var dotFile = Path.Combine(srcDir, ".env");
        var topFile = Path.Combine(srcDir, "plain note.txt");

        await File.WriteAllTextAsync(nestedFile, "line1\r\n中文内容\r\n", new UTF8Encoding(false));
        await File.WriteAllTextAsync(dotFile, "SECRET=local-test-only\n", new UTF8Encoding(false));
        await File.WriteAllTextAsync(topFile, "top content", new UTF8Encoding(false));

        var archive = Path.Combine(_root, $"bundle-{SftpTransfer.Stamp()}.tar.gz");
        var packErr = await SftpTransfer.PackLocalAsync(archive, new[] { subDir, dotFile, topFile });
        Assert.Null(packErr);
        Assert.True(File.Exists(archive));

        var extractErr = await SftpTransfer.ExtractLocalAsync(archive, stageDir);
        Assert.Null(extractErr);

        foreach (var entry in Directory.EnumerateFileSystemEntries(stageDir))
        {
            var target = Path.Combine(destDir, Path.GetFileName(entry));
            SftpTransfer.MoveEntryWithCrossVolumeFallback(entry, target, forceCopyFallback: true);
        }

        Assert.Equal("line1\r\n中文内容\r\n", await File.ReadAllTextAsync(Path.Combine(destDir, "sub dir", "hello world.txt")));
        Assert.Equal("SECRET=local-test-only\n", await File.ReadAllTextAsync(Path.Combine(destDir, ".env")));
        Assert.Equal("top content", await File.ReadAllTextAsync(Path.Combine(destDir, "plain note.txt")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(stageDir));
    }

    [Fact]
    public async Task PackLocalArchiveReturnsErrorWhenSourceDoesNotExist()
    {
        var archive = Path.Combine(_root, "missing.tar.gz");
        var missing = Path.Combine(_root, "does-not-exist.txt");

        var err = await SftpTransfer.PackLocalAsync(archive, new[] { missing });
        Assert.False(string.IsNullOrWhiteSpace(err));
    }
}
