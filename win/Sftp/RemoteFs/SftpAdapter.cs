using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PixShell.Logging;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace PixShell.Sftp.RemoteFs;

/// <summary>
/// 包装 SSH.NET SftpClient，直接委托。
/// </summary>
internal class SftpAdapter : IRemoteFs
{
    private readonly SftpClient _sftp;

    public bool Connected => _sftp.IsConnected;
    public string WorkingDirectory => _sftp.WorkingDirectory;
    public bool SupportsTree => true;
    public bool SupportsResumableTransfers => true;

    public SftpAdapter(SftpClient sftp)
    {
        _sftp = sftp;
    }

    public List<FsRow> ListDirectory(string path)
    {
        if (!Connected) return new();
        return _sftp.ListDirectory(path)
            .Where(f => f.Name != "." && f.Name != "..")
            .Select(f =>
            {
                var mode = ExtractPosixMode(f.Attributes);
                return new FsRow
                {
                    Name = f.Name,
                    IsDir = f.IsDirectory,
                    IsLink = f.IsSymbolicLink,
                    Size = f.IsDirectory ? 0 : f.Length,
                    Mtime = f.LastWriteTime,
                    Perms = mode ?? 0u,
                    HasPerms = mode.HasValue,
                };
            })
            .OrderByDescending(r => r.IsDir)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public List<FsRow>? ListTreeChildren(string path)
    {
        if (!Connected) return null;
        return _sftp.ListDirectory(path)
            .Where(f => f.IsDirectory && f.Name != "." && f.Name != "..")
            .Select(f => new FsRow { Name = f.Name, IsDir = true, Size = 0 })
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
    public bool Exists(string path) => _sftp.Exists(path);
    public long GetFileSize(string path) => _sftp.GetAttributes(path).Size;

    public void DownloadFile(string remotePath, Stream localStream, long remoteOffset = 0, CancellationToken cancellationToken = default)
    {
        using var remote = _sftp.Open(remotePath, FileMode.Open, FileAccess.Read);
        remote.Position = remoteOffset;
        Copy(remote, localStream, cancellationToken);
    }

    /// <summary>编辑器读取走 SSH.NET 原生异步 API，使断开时的 CancellationToken 能传到底层 SFTP 请求。</summary>
    public Task DownloadFileAsync(string remotePath, Stream localStream, CancellationToken cancellationToken) =>
        _sftp.DownloadFileAsync(remotePath, localStream, cancellationToken);

    public void UploadFile(Stream localStream, string remotePath, long remoteOffset = 0, CancellationToken cancellationToken = default)
    {
        var mode = remoteOffset == 0 ? FileMode.Create : FileMode.OpenOrCreate;
        using var remote = _sftp.Open(remotePath, mode, FileAccess.Write);
        remote.Position = remoteOffset;
        Copy(localStream, remote, cancellationToken);
    }

    private static void Copy(Stream source, Stream destination, CancellationToken token)
    {
        var buffer = new byte[32 * 1024];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            token.ThrowIfCancellationRequested();
            destination.Write(buffer, 0, read);
        }
    }

    public void CreateEmptyFile(string path)
    {
        using var file = _sftp.Open(path, FileMode.CreateNew, FileAccess.Write);
    }

    public void MakeDirectory(string path) => _sftp.CreateDirectory(path);
    public void Delete(string path, bool isDir) { if (isDir) _sftp.DeleteDirectory(path); else _sftp.DeleteFile(path); }
    public void Rename(string from, string to) => _sftp.RenameFile(from, to);

    /// <summary>提取远端条目完整 12 位 POSIX 权限（含 setuid/setgid/sticky 与 rwxrwxrwx）。</summary>
    internal static uint? ExtractPosixMode(SftpFileAttributes? a)
    {
        if (a == null) return null;
        uint m = 0;
        if (a.IsUIDBitSet) m |= 0x800;
        if (a.IsGroupIDBitSet) m |= 0x400;
        if (a.IsStickyBitSet) m |= 0x200;
        if (a.OwnerCanRead) m |= 0x100;
        if (a.OwnerCanWrite) m |= 0x80;
        if (a.OwnerCanExecute) m |= 0x40;
        if (a.GroupCanRead) m |= 0x20;
        if (a.GroupCanWrite) m |= 0x10;
        if (a.GroupCanExecute) m |= 0x8;
        if (a.OthersCanRead) m |= 0x4;
        if (a.OthersCanWrite) m |= 0x2;
        if (a.OthersCanExecute) m |= 0x1;
        return m;
    }

    internal uint? TryGetPosixMode(string path)
    {
        try
        {
            return ExtractPosixMode(_sftp.GetAttributes(path));
        }
        catch
        {
            return null;
        }
    }

    internal bool TrySetPosixMode(string path, uint mode)
    {
        try
        {
            _sftp.ChangePermissions(path, (short)(mode & 0xFFFu));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Close()
    {
        Log.Info("SFTP client 关闭开始", "sftp");
        try { if (Connected) _sftp.Disconnect(); } catch { }
        try { _sftp.Dispose(); } catch { }
        Log.Info("SFTP client 关闭完成", "sftp");
    }

    public void Dispose() => Close();
}
