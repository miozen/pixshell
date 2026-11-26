using System;
using System.IO;
using System.Threading;

namespace PixShell.Sftp;

/// <summary>
/// 远端文件安全覆写所需的最小文件原语抽象，便于统一适配三种远端后端与单元测试。
/// </summary>
public interface IAtomicRemoteFileStore
{
    bool Connected { get; }
    bool VerifyUploadedSize { get; }
    /// <summary>后端 Rename(temp, target) 是否支持单步原子覆盖已存在目标（如 POSIX mv）。</summary>
    bool SupportsAtomicOverwriteRename { get; }
    bool Exists(string path);
    long GetFileSize(string path);
    uint? TryGetPosixMode(string path);
    bool TrySetPosixMode(string path, uint mode);
    void Upload(Stream localStream, string remotePath, CancellationToken cancellationToken = default);
    void Rename(string from, string to);
    void Delete(string path, bool isDir);
}

/// <summary>
/// 校验远端编辑保存时的会话绑定：
/// 单纯切换到其他标签后再切回原标签（同一 TerminalSession 仍存活且当前 SFTP 已就绪）允许继续保存；
/// 停留在其他标签或原会话已断开时阻止写入并保留编辑器内容。
/// </summary>
public static class RemoteEditTargetGuard
{
    public static (TStore? TargetStore, string? Error) ResolveSaveTarget<TStore>(
        object? expectedSession,
        TStore? expectedRemoteFs,
        bool expectedSessionConnected,
        bool expectedFsConnected,
        object? currentSession,
        TStore? currentRemoteFs,
        bool currentFsConnected,
        string? targetLabel) where TStore : class
    {
        var label = string.IsNullOrWhiteSpace(targetLabel) ? "原连接" : targetLabel;
        if (expectedSession == null || !expectedSessionConnected)
            return (null, $"原会话已断开或关闭（目标：{label}），已阻止写入；编辑器内容已保留");

        if (!ReferenceEquals(expectedSession, currentSession))
            return (null, $"当前已切换到其他会话标签，原标签（{label}）的 SFTP 连接已在切签时释放；请切回原标签后再保存（编辑器内容已保留）");

        if (expectedRemoteFs != null && ReferenceEquals(expectedRemoteFs, currentRemoteFs) && expectedFsConnected)
            return (expectedRemoteFs, null);

        if (currentRemoteFs != null && currentFsConnected)
            return (currentRemoteFs, null);

        return (null, $"原标签（{label}）的 SFTP 尚未重新连接就绪，暂无法保存；请稍后重试（编辑器内容已保留）");
    }
}

/// <summary>
/// 远端文件安全写回：先上传至同目录临时文件、校验字节数并同步原文件权限；
/// 支持单步原子覆盖的后端直接 Rename(temp, target)，否则采用可回滚备份交换策略。
/// 绝不在新文件完整上传前删除或截断既有远端目标文件，失败时保留并回报备份路径。
/// </summary>
public static class RemoteFileOverwrite
{
    public static void SaveBytes(
        IAtomicRemoteFileStore store,
        string remotePath,
        byte[] bytes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(remotePath))
            throw new ArgumentException("远端路径不能为空", nameof(remotePath));
        if (!store.Connected)
            throw new IOException("远端未连接");

        var id = Guid.NewGuid().ToString("N");
        var tempPath = $"{remotePath}.pixshell.tmp.{id}";
        var backupPath = $"{remotePath}.pixshell.bak.{id}";
        var tempCreatedAttempted = false;
        var movedOriginalToBackup = false;

        try
        {
            var targetExists = store.Exists(remotePath);
            uint? originalMode = targetExists ? store.TryGetPosixMode(remotePath) : null;

            tempCreatedAttempted = true;
            using (var ms = new MemoryStream(bytes, writable: false))
            {
                store.Upload(ms, tempPath, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (store.VerifyUploadedSize)
            {
                var remoteSize = store.GetFileSize(tempPath);
                if (remoteSize != bytes.Length)
                    throw new IOException($"临时文件校验失败：期望 {bytes.Length} 字节，实际 {remoteSize} 字节");
            }

            if (originalMode.HasValue)
            {
                var modeSet = store.TrySetPosixMode(tempPath, originalMode.Value);
                var hadRestrictedPermissions = (originalMode.Value & 0x03Fu) == 0;
                if (!modeSet && hadRestrictedPermissions)
                {
                    throw new IOException(
                        $"无法恢复原文件受限权限 {Convert.ToString(originalMode.Value, 8)}，已中止替换以免权限放宽");
                }
            }

            if (targetExists && !store.SupportsAtomicOverwriteRename)
            {
                store.Rename(remotePath, backupPath);
                movedOriginalToBackup = true;
            }

            store.Rename(tempPath, remotePath);
            tempCreatedAttempted = false;

            if (movedOriginalToBackup)
            {
                movedOriginalToBackup = false;
                try { store.Delete(backupPath, isDir: false); } catch { /* 主目标已替换成功，残留备份不阻塞 */ }
            }
        }
        catch (Exception saveEx)
        {
            if (movedOriginalToBackup)
            {
                try
                {
                    store.Rename(backupPath, remotePath);
                }
                catch (Exception rollbackEx)
                {
                    throw new IOException(
                        $"写回失败（{saveEx.Message}），且自动回滚原文件失败（{rollbackEx.Message}）；原文件备份仍保留在远端：{backupPath}",
                        saveEx);
                }
            }
            throw;
        }
        finally
        {
            if (tempCreatedAttempted && store.Connected)
            {
                try { store.Delete(tempPath, isDir: false); } catch { }
            }
        }
    }
}
