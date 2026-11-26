using System.Text;
using PixShell.Sftp;
using Xunit;

namespace PixShell.Tests;

public sealed class SftpEditorAndRefreshTests
{
    private sealed class InMemoryRemoteFileStore : IAtomicRemoteFileStore
    {
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, uint> Modes { get; } = new(StringComparer.Ordinal);
        public bool Connected { get; set; } = true;
        public bool VerifyUploadedSize { get; set; } = true;
        public bool SupportsAtomicOverwriteRename { get; set; }
        public Action<string>? BeforeRename { get; set; }
        public bool FailOnTempUpload { get; set; }
        public bool FailOnSetPosixMode { get; set; }
        public long? OverrideTempFileSize { get; set; }
        public int RenameCount { get; private set; }
        public bool UsedBackupPath { get; private set; }

        public bool Exists(string path) => Files.ContainsKey(path);

        public long GetFileSize(string path)
        {
            if (path.Contains(".pixshell.tmp.", StringComparison.Ordinal) && OverrideTempFileSize.HasValue)
                return OverrideTempFileSize.Value;
            return Files.TryGetValue(path, out var data) ? data.Length : -1;
        }

        public uint? TryGetPosixMode(string path) =>
            Modes.TryGetValue(path, out var m) ? m : null;

        public bool TrySetPosixMode(string path, uint mode)
        {
            if (FailOnSetPosixMode || !Files.ContainsKey(path)) return false;
            Modes[path] = mode;
            return true;
        }

        public void Upload(Stream localStream, string remotePath, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            localStream.CopyTo(ms);
            if (FailOnTempUpload)
            {
                var partial = ms.ToArray();
                Files[remotePath] = partial.Length > 0 ? partial[..Math.Max(1, partial.Length / 2)] : Array.Empty<byte>();
                Modes[remotePath] = 0x1A4;
                throw new IOException("simulated mid-upload disconnect");
            }

            cancellationToken.ThrowIfCancellationRequested();
            Files[remotePath] = ms.ToArray();
            Modes[remotePath] = 0x1A4; // simulate umask 0644
        }

        public void Rename(string from, string to)
        {
            RenameCount++;
            if (to.Contains(".pixshell.bak.", StringComparison.Ordinal)) UsedBackupPath = true;
            BeforeRename?.Invoke(from);
            if (!Files.TryGetValue(from, out var data))
                throw new FileNotFoundException("source missing", from);
            if (!SupportsAtomicOverwriteRename && Files.ContainsKey(to))
                throw new IOException("destination already exists (SFTP status 11)");
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

    // ==================== WIN-BUG-029 ====================

    [Fact]
    public void OutOfOrderDirectoryRefreshResultIsRejected()
    {
        var coordinator = new RemoteDirectoryRefreshCoordinator();
        var epoch = new SftpConnectionEpoch();
        var connGen = epoch.Advance();

        var ticketA = coordinator.Begin("/dirA", connGen, out var tokenA);
        var ticketB = coordinator.Begin("/dirB", connGen, out var tokenB);

        Assert.True(tokenA.IsCancellationRequested);
        Assert.False(tokenB.IsCancellationRequested);
        Assert.False(coordinator.CanApply(ticketA, connGen, remoteConnected: true));
        Assert.False(coordinator.CanShowFailure(ticketA, connGen));
        Assert.True(coordinator.CanApply(ticketB, connGen, remoteConnected: true));
    }

    [Fact]
    public void SessionSwitchInvalidatesInFlightRefreshAndOperationSnapshots()
    {
        var coordinator = new RemoteDirectoryRefreshCoordinator();
        var epoch = new SftpConnectionEpoch();
        var gen1 = epoch.Advance();
        var sessionA = new object();
        var sessionB = new object();
        var fsA = new object();
        var fsB = new object();

        var ticket = coordinator.Begin("/var/log", gen1, out var token);
        var deleteSnap = new RemoteDirectoryRefreshCoordinator.OperationSnapshot(sessionA, fsA, gen1, "/var/log");

        coordinator.Invalidate();
        var gen2 = epoch.Advance();

        Assert.True(token.IsCancellationRequested);
        Assert.False(coordinator.CanApply(ticket, gen2, remoteConnected: true));
        Assert.False(coordinator.CanShowFailure(ticket, gen2));
        Assert.False(RemoteDirectoryRefreshCoordinator.CanExecuteOperation(
            deleteSnap, sessionB, fsB, gen2, "/var/log", remoteConnected: true));
        Assert.False(RemoteDirectoryRefreshCoordinator.CanExecuteOperation(
            deleteSnap, sessionA, fsA, gen1, "/etc", remoteConnected: true));
        Assert.True(RemoteDirectoryRefreshCoordinator.CanExecuteOperation(
            new RemoteDirectoryRefreshCoordinator.OperationSnapshot(sessionA, fsA, gen1, "/var/log"),
            sessionA, fsA, gen1, "/var/log", remoteConnected: true));
    }

    // ==================== WIN-BUG-001A ====================

    [Fact]
    public void ShorterSaveOverwritesCleanlyAndPreservesPosixPermissions()
    {
        var store = new InMemoryRemoteFileStore();
        store.Files["/usr/local/bin/deploy.sh"] = Encoding.UTF8.GetBytes(new string('A', 120));
        store.Modes["/usr/local/bin/deploy.sh"] = 0x1ED; // 0755

        var shorter = Encoding.UTF8.GetBytes("#!/bin/sh\necho ok\n");
        RemoteFileOverwrite.SaveBytes(store, "/usr/local/bin/deploy.sh", shorter);

        Assert.Single(store.Files);
        Assert.Equal(shorter, store.Files["/usr/local/bin/deploy.sh"]);
        Assert.Equal((uint)0x1ED, store.Modes["/usr/local/bin/deploy.sh"]);
    }

    [Fact]
    public void AtomicOverwriteBackendUsesSingleStepRenameWithoutBackupWindow()
    {
        var store = new InMemoryRemoteFileStore { SupportsAtomicOverwriteRename = true };
        store.Files["/etc/app.conf"] = Encoding.UTF8.GetBytes("old");
        store.Modes["/etc/app.conf"] = 0x180; // 0600

        var updated = Encoding.UTF8.GetBytes("new-atomic");
        RemoteFileOverwrite.SaveBytes(store, "/etc/app.conf", updated);

        Assert.Single(store.Files);
        Assert.Equal(updated, store.Files["/etc/app.conf"]);
        Assert.Equal((uint)0x180, store.Modes["/etc/app.conf"]);
        Assert.False(store.UsedBackupPath);
        Assert.Equal(1, store.RenameCount);
    }

    [Fact]
    public void RestrictedModeRestoreFailureAbortsWithoutWideningPermissionsOrReplacingTarget()
    {
        var store = new InMemoryRemoteFileStore { FailOnSetPosixMode = true };
        var original = Encoding.UTF8.GetBytes("SECRET=abc");
        store.Files["/etc/.env"] = original;
        store.Modes["/etc/.env"] = 0x180; // 0600

        Assert.Throws<IOException>(() =>
            RemoteFileOverwrite.SaveBytes(store, "/etc/.env", Encoding.UTF8.GetBytes("SECRET=xyz")));

        Assert.Equal(original, store.Files["/etc/.env"]);
        Assert.Equal((uint)0x180, store.Modes["/etc/.env"]);
        Assert.Single(store.Files);
    }

    [Fact]
    public void MidUploadFailureAndCancellationLeaveOriginalFileIntactAndCleanTemp()
    {
        var store = new InMemoryRemoteFileStore { FailOnTempUpload = true };
        var original = Encoding.UTF8.GetBytes("original-important-config");
        store.Files["/etc/nginx.conf"] = original;

        Assert.Throws<IOException>(() =>
            RemoteFileOverwrite.SaveBytes(store, "/etc/nginx.conf", Encoding.UTF8.GetBytes("short")));
        Assert.Equal(original, store.Files["/etc/nginx.conf"]);
        Assert.Single(store.Files);

        var cancelStore = new InMemoryRemoteFileStore();
        cancelStore.Files["/etc/nginx.conf"] = original;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            RemoteFileOverwrite.SaveBytes(cancelStore, "/etc/nginx.conf", Encoding.UTF8.GetBytes("new"), cts.Token));
        Assert.Equal(original, cancelStore.Files["/etc/nginx.conf"]);
        Assert.Single(cancelStore.Files);
    }

    [Fact]
    public void SizeVerificationMismatchAbortsBeforeTouchingOriginalFile()
    {
        var store = new InMemoryRemoteFileStore { OverrideTempFileSize = 3 };
        var original = Encoding.UTF8.GetBytes("keep-intact");
        store.Files["/etc/hosts"] = original;

        Assert.Throws<IOException>(() =>
            RemoteFileOverwrite.SaveBytes(store, "/etc/hosts", Encoding.UTF8.GetBytes("127.0.0.1 localhost")));

        Assert.Equal(original, store.Files["/etc/hosts"]);
        Assert.Single(store.Files);
    }

    [Fact]
    public void SwapRenameFailureRollsBackAndDoubleFailureSurfacesBackupPath()
    {
        var store = new InMemoryRemoteFileStore();
        var original = Encoding.UTF8.GetBytes("old-content");
        store.Files["/etc/motd"] = original;
        store.BeforeRename = from =>
        {
            if (from.Contains(".pixshell.tmp.", StringComparison.Ordinal))
                throw new IOException("simulated permission failure during final rename");
        };

        Assert.Throws<IOException>(() =>
            RemoteFileOverwrite.SaveBytes(store, "/etc/motd", Encoding.UTF8.GetBytes("new-content")));
        Assert.Equal(original, store.Files["/etc/motd"]);
        Assert.Single(store.Files);

        var doubleFailStore = new InMemoryRemoteFileStore();
        doubleFailStore.Files["/etc/motd"] = original;
        doubleFailStore.BeforeRename = from =>
        {
            if (from.Contains(".pixshell.tmp.", StringComparison.Ordinal))
                throw new IOException("final rename denied");
            if (from.Contains(".pixshell.bak.", StringComparison.Ordinal))
                throw new IOException("rollback rename denied");
        };

        var err = Assert.Throws<IOException>(() =>
            RemoteFileOverwrite.SaveBytes(doubleFailStore, "/etc/motd", Encoding.UTF8.GetBytes("new-content")));
        Assert.Contains(".pixshell.bak.", err.Message);
        Assert.Contains(doubleFailStore.Files.Keys, k => k.Contains(".pixshell.bak.", StringComparison.Ordinal));
        Assert.DoesNotContain(doubleFailStore.Files.Keys, k => k.Contains(".pixshell.tmp.", StringComparison.Ordinal));
    }

    [Fact]
    public void CreatingNewFileViaAtomicSaveWritesExactBytes()
    {
        var store = new InMemoryRemoteFileStore();
        var payload = Encoding.UTF8.GetBytes("hello=world\n");

        RemoteFileOverwrite.SaveBytes(store, "/tmp/brandnew.env", payload);

        Assert.Single(store.Files);
        Assert.Equal(payload, store.Files["/tmp/brandnew.env"]);
    }

    // ==================== WIN-BUG-001B ====================

    [Fact]
    public void SaveTargetGuardBlocksInactiveTabButAllowsSaveAfterReturningToOriginalSession()
    {
        var sessionA = new object();
        var sessionB = new object();
        var fsA1 = new object();
        var fsA2 = new object();
        var fsB = new object();

        var same = RemoteEditTargetGuard.ResolveSaveTarget(
            sessionA, fsA1, expectedSessionConnected: true, expectedFsConnected: true,
            sessionA, fsA1, currentFsConnected: true, "prod-A");
        Assert.Same(fsA1, same.TargetStore);
        Assert.Null(same.Error);

        var whileOnTabB = RemoteEditTargetGuard.ResolveSaveTarget(
            sessionA, fsA1, expectedSessionConnected: true, expectedFsConnected: false,
            sessionB, fsB, currentFsConnected: true, "prod-A");
        Assert.Null(whileOnTabB.TargetStore);
        Assert.Contains("请切回原标签", whileOnTabB.Error);

        var backOnTabAReconnected = RemoteEditTargetGuard.ResolveSaveTarget(
            sessionA, fsA1, expectedSessionConnected: true, expectedFsConnected: false,
            sessionA, fsA2, currentFsConnected: true, "prod-A");
        Assert.Same(fsA2, backOnTabAReconnected.TargetStore);
        Assert.Null(backOnTabAReconnected.Error);

        var sessionDisconnected = RemoteEditTargetGuard.ResolveSaveTarget(
            sessionA, fsA1, expectedSessionConnected: false, expectedFsConnected: false,
            sessionA, fsA2, currentFsConnected: false, "prod-A");
        Assert.Null(sessionDisconnected.TargetStore);
        Assert.NotNull(sessionDisconnected.Error);
    }

    // ==================== WIN-BUG-001C & WIN-BUG-014A ====================

    [Fact]
    public void EncodingRoundTripPreservesUtf8Utf16AndUtf32BomsAndRejectsCorruptBom()
    {
        var plainUtf8 = Encoding.UTF8.GetBytes("echo 你好\n");
        var plainResult = RemoteTextEncoding.Decode(plainUtf8);
        Assert.Equal("UTF-8", plainResult.Metadata.Name);
        Assert.False(plainResult.Metadata.HasBom);
        Assert.Equal(plainUtf8, RemoteTextEncoding.Encode(plainResult.Text, plainResult.Metadata));

        var utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("key=值\n")).ToArray();
        var utf8BomResult = RemoteTextEncoding.Decode(utf8Bom);
        Assert.Equal("UTF-8 BOM", utf8BomResult.Metadata.Name);
        Assert.True(utf8BomResult.Metadata.HasBom);
        Assert.Equal(utf8Bom, RemoteTextEncoding.Encode(utf8BomResult.Text, utf8BomResult.Metadata));

        var utf32Le = new byte[] { 0xFF, 0xFE, 0x00, 0x00 }
            .Concat(new UTF32Encoding(bigEndian: false, byteOrderMark: false).GetBytes("hi\n"))
            .ToArray();
        var utf32Result = RemoteTextEncoding.Decode(utf32Le);
        Assert.Equal("UTF-32 LE", utf32Result.Metadata.Name);
        Assert.True(utf32Result.Metadata.HasBom);
        Assert.Equal(utf32Le, RemoteTextEncoding.Encode(utf32Result.Text, utf32Result.Metadata));

        var corruptUtf8Bom = new byte[] { 0xEF, 0xBB, 0xBF, 0xFF, 0xFE };
        Assert.Throws<InvalidDataException>(() => RemoteTextEncoding.Decode(corruptUtf8Bom));
    }

    [Fact]
    public void EncodingPreservesGb18030AndLatin1WithoutReplacementCorruption()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var gb = Encoding.GetEncoding(54936);
        var gbBytes = gb.GetBytes("配置=中文测试\n");

        var gbResult = RemoteTextEncoding.Decode(gbBytes);
        Assert.Equal("GB18030", gbResult.Metadata.Name);
        Assert.Equal("配置=中文测试\n", gbResult.Text);
        Assert.DoesNotContain('\uFFFD', gbResult.Text);
        Assert.Equal(gbBytes, RemoteTextEncoding.Encode(gbResult.Text, gbResult.Metadata));

        var latin1Bytes = new byte[] { (byte)'c', (byte)'a', (byte)'f', 0xE9, (byte)'\n' };
        var latin1Result = RemoteTextEncoding.Decode(latin1Bytes);
        Assert.Equal("ISO-8859-1", latin1Result.Metadata.Name);
        Assert.Equal(latin1Bytes, RemoteTextEncoding.Encode(latin1Result.Text, latin1Result.Metadata));

        Assert.Throws<InvalidOperationException>(() =>
            RemoteTextEncoding.Encode(latin1Result.Text + "中文", latin1Result.Metadata));
    }

    [Fact]
    public void EncodingPreservesPureCrlfAndMixedNewlinesAndRejectsBinaryNulFiles()
    {
        var crlfBytes = Encoding.UTF8.GetBytes("line1\r\nline2\r\n");
        var decoded = RemoteTextEncoding.Decode(crlfBytes);
        Assert.True(decoded.Metadata.PreserveCrlf);
        var editorNormalized = decoded.Text.Replace("\r\n", "\n");
        Assert.Equal(crlfBytes, RemoteTextEncoding.Encode(editorNormalized, decoded.Metadata));

        var mixedBytes = Encoding.UTF8.GetBytes("line1\r\nline2\nline3\r\n");
        var mixedDecoded = RemoteTextEncoding.Decode(mixedBytes);
        Assert.False(mixedDecoded.Metadata.PreserveCrlf);
        Assert.Equal(mixedBytes, RemoteTextEncoding.Encode(mixedDecoded.Text, mixedDecoded.Metadata));

        var binaryBytes = new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F', 0x00, 0x02 };
        Assert.Throws<InvalidDataException>(() => RemoteTextEncoding.Decode(binaryBytes));
    }
}
