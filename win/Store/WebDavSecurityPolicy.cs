using System;

namespace PixShell.Store;

/// <summary>
/// WebDAV 传输安全校验（WIN-BUG-018A）：
/// Basic 认证会携带 Base64(username:password)，禁止向非回环明文 HTTP 远端发送凭据。
/// </summary>
public static class WebDavSecurityPolicy
{
    public static bool ValidateEndpoint(string? rawUrl, out Uri? uri, out string? error)
    {
        if (string.IsNullOrWhiteSpace(rawUrl) || !Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out uri))
        {
            uri = null;
            error = "URL 无效";
            return false;
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = null;
            return true;
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.IsLoopback)
        {
            error = null;
            return true;
        }

        uri = null;
        error = "出于凭据安全考虑，WebDAV Basic 认证仅支持 HTTPS（仅本机回环地址允许 HTTP）。";
        return false;
    }
}
