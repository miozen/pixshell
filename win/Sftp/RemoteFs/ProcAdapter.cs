using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace PixShell.Sftp.RemoteFs;

/// <summary>
/// 包装 ProcSftpClient（ssh exec sftp-server → 原生 SFTP v3）。
/// 无目录树，WorkingDirectory 固定为 /。
/// </summary>
internal class ProcAdapter : IRemoteFs
{
    private readonly ProcSftpClient _proc;

    public bool Connected => _proc.Connected;
    public string WorkingDirectory => "/";
    public bool SupportsTree => false;
    public bool SupportsResumableTransfers => true;

    public ProcAdapter(ProcSftpClient proc) { _proc = proc; }

    public List<FsRow> ListDirectory(string path) => _proc.ListDirectory(path);
    public bool Exists(string path) => _proc.Exists(path);
    public long GetFileSize(string path) => _proc.GetFileSize(path);
    public List<FsRow>? ListTreeChildren(string path) => null;
    public void DownloadFile(string remotePath, Stream localStream, long remoteOffset = 0, CancellationToken cancellationToken = default) => _proc.DownloadFile(remotePath, localStream, remoteOffset, cancellationToken);
    public void UploadFile(Stream localStream, string remotePath, long remoteOffset = 0, CancellationToken cancellationToken = default) => _proc.UploadFile(localStream, remotePath, remoteOffset, cancellationToken);
    public void CreateEmptyFile(string path) => _proc.CreateEmptyFile(path);
    public void MakeDirectory(string path) => _proc.MakeDirectory(path);
    public void Delete(string path, bool isDir) => _proc.Delete(path, isDir);
    public void Rename(string from, string to) => _proc.Rename(from, to);
    public void Close() => _proc.Close();
    public void Dispose() => _proc.Dispose();
}
