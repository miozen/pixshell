namespace PixShell.Sftp;

/// <summary>
/// 绑定单次远端打开编辑的上下文，确保保存只写回原始会话与远端后端并保留原文件编码。
/// </summary>
public sealed record RemoteEditDocument(
    string RemotePath,
    string Text,
    TerminalSession? Session,
    RemoteFs.IRemoteFs RemoteFs,
    RemoteTextEncoding.EncodingMetadata EncodingInfo,
    string SessionLabel);
