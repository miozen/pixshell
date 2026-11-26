using System.Text;
using PixShell.Sftp;
using Xunit;

namespace PixShell.Tests;

public sealed class SftpScpAndTransferTests
{
    private sealed class DirectUploadTestStore : IAtomicRemoteFileStore
    {
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, uint> Modes { get; } = new(StringComparer.Ordinal);
        public bool Connected { get; set; } = true;
        public bool VerifyUploadedSize => true;
        public bool SupportsAtomicOverwriteRename { get; set; }
        public bool FailFinalRename { get; set; }

        public bool Exists(string path) => Files.ContainsKey(path);
        public long GetFileSize(string path) => Files.TryGetValue(path, out var data) ? data.Length : -1;
        public uint? TryGetPosixMode(string path) => Modes.TryGetValue(path, out var m) ? m : null;

        public bool TrySetPosixMode(string path, uint mode)
        {
            if (!Files.ContainsKey(path)) return false;
            Modes[path] = mode;
            return true;
        }

        public void Upload(Stream localStream, string remotePath, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            localStream.CopyTo(ms);
            Files[remotePath] = ms.ToArray();
            Modes[remotePath] = 0x1A4;
        }

        public void Rename(string from, string to)
        {
            if (FailFinalRename && from.Contains(".pixshell.part.", StringComparison.Ordinal))
                throw new IOException("simulated permission failure during replace");
            if (!Files.TryGetValue(from, out var data))
                throw new FileNotFoundException("missing source", from);
            if (!SupportsAtomicOverwriteRename && Files.ContainsKey(to))
                throw new IOException("SFTP status 11: target already exists");
            Files.Remove(from);
            Files[to] = data;
            if (Modes.Remove(from, out var mode))
                Modes[to] = mode;
        }

        public void Delete(string path, bool isDir)
        {
            Files.Remove(path);
            Modes.Remove(path);
        }
    }

    // ==================== WIN-BUG-003A ====================

    [Fact]
    public void ExtractStagedCommandChecksMvExitCodeAndHandlesDoubleDotFiles()
    {
        var cmd = SftpTransfer.ExtractStagedCommand("/tmp/up.tar.gz", "/srv/app", "/tmp/stage");

        Assert.Contains("mv_err=$(mv \"$p\" '/srv/app'/ 2>&1)", cmd);
        Assert.Contains("mrc=$?", cmd);
        Assert.Contains("rc=$mrc", cmd);
        Assert.Contains("MV_ERR:$n:$mv_err", cmd);
        Assert.Contains("/..?*", cmd);
        Assert.Contains("[ -L \"$p\" ]", cmd);

        var (rc, msg) = SftpTransfer.ParseRemoteRC("MV_ERR:app.bin:mv: permission denied\n__PIXSHELL_RC:1");
        Assert.Equal(1, rc);
        Assert.Contains("MV_ERR:app.bin", msg);

        var (missingRc, _) = SftpTransfer.ParseRemoteRC("");
        Assert.NotEqual(0, missingRc);
    }

    // ==================== WIN-BUG-004A / 004B / 015A ====================

    [Fact]
    public void ScpLsParserParsesIsoBusyBoxAndTraditionalListingsWithSpacesAndSymlinks()
    {
        const string listing = """
            total 32
            drwxr-xr-x 2 root root 4096 2025-10-24 12:34 .
            drwxr-xr-x 4 root root 4096 2025-10-24 12:34 ..
            -rw------- 1 app  app   512 2025-10-24 12:34 .env secret
            drwxr-sr-x 2 app  app  4096 Oct 24 12:34 dir with  spaces
            -rwsr-xr-x 1 root root 1024 Oct  4  2025 setuid tool
            lrwxrwxrwx 1 app  app    18 Oct 24 09:15 my link -> /var/log/target file
            drwxr-xr-t+ 2 root root 4096 2025-10-24 12:34:05 +0800  leading_space
            """;

        var entries = ScpLsParser.ParseListing(listing);

        Assert.Equal(5, entries.Count);

        var env = entries.Single(e => e.Name == ".env secret");
        Assert.False(env.IsDir);
        Assert.Equal(512, env.Size);
        Assert.Equal((uint)0x180, env.Perms); // 0600
        Assert.True(env.HasPerms);

        var spacedDir = entries.Single(e => e.Name == "dir with  spaces");
        Assert.True(spacedDir.IsDir);
        Assert.Equal((uint)0x400 | 0x1ED, spacedDir.Perms); // 2755

        var setuid = entries.Single(e => e.Name == "setuid tool");
        Assert.Equal(1024, setuid.Size);
        Assert.Equal((uint)0x800 | 0x1ED, setuid.Perms); // 4755

        var link = entries.Single(e => e.Name == "my link");
        Assert.True(link.IsLink);
        Assert.Equal(18, link.Size);

        var leading = entries.Single(e => e.Name == " leading_space");
        Assert.True(leading.IsDir);
        Assert.Equal((uint)0x200 | 0x1ED, leading.Perms); // 1755
    }

    [Fact]
    public void ScpLsParserDoesNotMisinterpretErrorOutputAsEntries()
    {
        const string errorBody = "ls: cannot access '/root/secret': Permission denied";
        Assert.Empty(ScpLsParser.ParseListing(errorBody));

        var (rc, msg) = SftpTransfer.ParseRemoteRC(errorBody + "\n__PIXSHELL_RC:2");
        Assert.Equal(2, rc);
        Assert.Contains("Permission denied", msg);
    }

    // ==================== WIN-BUG-009A ====================

    [Fact]
    public void DirectUploadOverwriteSucceedsAcrossNonAtomicSftpAndRollsBackOnFailure()
    {
        var store = new DirectUploadTestStore { SupportsAtomicOverwriteRename = false };
        var existing = Encoding.UTF8.GetBytes("old-server-binary");
        var temp = "/var/www/app.bin.pixshell.part.1234";
        var updated = Encoding.UTF8.GetBytes("new-release");
        store.Files["/var/www/app.bin"] = existing;
        store.Modes["/var/www/app.bin"] = 0x1ED; // 0755
        store.Files[temp] = updated;
        store.Modes[temp] = 0x1A4; // 0644

        RemoteFileOverwrite.ReplaceUploadedTemp(store, temp, "/var/www/app.bin");

        Assert.Single(store.Files);
        Assert.Equal(updated, store.Files["/var/www/app.bin"]);
        Assert.Equal((uint)0x1ED, store.Modes["/var/www/app.bin"]);

        // Rollback when temp -> target rename fails must restore original file intact.
        var failStore = new DirectUploadTestStore { SupportsAtomicOverwriteRename = false, FailFinalRename = true };
        var failTemp = "/var/www/app.bin.pixshell.part.9999";
        failStore.Files["/var/www/app.bin"] = existing;
        failStore.Modes["/var/www/app.bin"] = 0x180;
        failStore.Files[failTemp] = updated;
        failStore.Modes[failTemp] = 0x1A4;

        Assert.Throws<IOException>(() =>
            RemoteFileOverwrite.ReplaceUploadedTemp(failStore, failTemp, "/var/www/app.bin"));
        Assert.Equal(existing, failStore.Files["/var/www/app.bin"]);
        Assert.Equal((uint)0x180, failStore.Modes["/var/www/app.bin"]);
    }
}
