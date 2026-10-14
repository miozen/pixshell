using System.Threading;

namespace PixShell.Sftp;

/// <summary>使在途 SFTP 建连结果失效，避免断开后旧连接回填到新会话。</summary>
public sealed class SftpConnectionEpoch
{
    private int _value;
    public int Advance() => Interlocked.Increment(ref _value);
    public bool IsCurrent(int value) => Volatile.Read(ref _value) == value;
}
