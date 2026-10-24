using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace PixShell.Sftp.RemoteFs;

/// <summary>
/// 统一文件后端接口。屏蔽 SFTP/SCP/ProcSftp 协议差异，
/// SftpPanel 只需要面对这一个抽象。
/// </summary>
public interface IRemoteFs : IDisposable
{
    bool Connected { get; }
    string WorkingDirectory { get; }
    bool SupportsResumableTransfers { get; }

    // ---- 文件操作 ----

    /// <summary>列出目录内容。</summary>
    List<FsRow> ListDirectory(string path);
    bool Exists(string path);
    long GetFileSize(string path);

    /// <summary>下载远端文件到流。</summary>
    void DownloadFile(string remotePath, Stream localStream, long remoteOffset = 0, CancellationToken cancellationToken = default);

    /// <summary>上传流到远端文件。</summary>
    void UploadFile(Stream localStream, string remotePath, long remoteOffset = 0, CancellationToken cancellationToken = default);

    // ---- 元操作 ----

    /// <summary>以独占方式创建空文件；目标已存在时必须失败，绝不覆盖。</summary>
    void CreateEmptyFile(string path);
    void MakeDirectory(string path);
    void Delete(string path, bool isDir);
    void Rename(string from, string to);

    // ---- 可选能力 ----

    /// <summary>是否支持递归目录树（SftpClient 有，SCP/Proc 无）。</summary>
    bool SupportsTree { get; }

    /// <summary>列出子目录（仅 SupportsTree=true 时可用）。</summary>
    List<FsRow>? ListTreeChildren(string path);
}
