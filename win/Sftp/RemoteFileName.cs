namespace PixShell.Sftp;

/// <summary>远端“新建文件”对单个文件名的最小校验，不把路径解析交给各后端。</summary>
public static class RemoteFileName
{
    public static bool TryNormalize(string? requested, out string name, out string error)
    {
        name = (requested ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            error = "请输入文件名";
            return false;
        }

        if (name is "." or "..")
        {
            error = "文件名不能是 . 或 ..";
            return false;
        }

        if (name.IndexOf('\0') >= 0 || name.Contains('/') || name.Contains('\\'))
        {
            error = "文件名不能包含路径分隔符或空字符";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
