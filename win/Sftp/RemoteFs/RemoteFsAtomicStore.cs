using System.IO;
using System.Threading;

namespace PixShell.Sftp.RemoteFs;

internal sealed class RemoteFsAtomicStore : IAtomicRemoteFileStore
{
    private readonly IRemoteFs _fs;

    public RemoteFsAtomicStore(IRemoteFs fs)
    {
        _fs = fs;
    }

    public bool Connected => _fs.Connected;
    public bool VerifyUploadedSize => _fs.SupportsResumableTransfers;
    public bool SupportsAtomicOverwriteRename => _fs is ScpAdapter;
    public bool Exists(string path) => _fs.Exists(path);
    public long GetFileSize(string path) => _fs.GetFileSize(path);
    public uint? TryGetPosixMode(string path) => _fs is SftpAdapter sftp ? sftp.TryGetPosixMode(path) : null;
    public bool TrySetPosixMode(string path, uint mode) => _fs is SftpAdapter sftp && sftp.TrySetPosixMode(path, mode);
    public void Upload(Stream localStream, string remotePath, CancellationToken cancellationToken = default)
        => _fs.UploadFile(localStream, remotePath, 0, cancellationToken);
    public void Rename(string from, string to) => _fs.Rename(from, to);
    public void Delete(string path, bool isDir) => _fs.Delete(path, isDir);
}
