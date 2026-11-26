using System;
using System.IO;
using System.Text;

namespace PixShell.Sftp;

/// <summary>
/// 远端文本文件的严格编码检测与无损回写：避免默认 UTF-8 吞掉非法字节成 U+FFFD，并保留原始编码与 BOM。
/// </summary>
public static class RemoteTextEncoding
{
    public sealed record EncodingMetadata(string Name, int CodePage, bool HasBom, bool PreserveCrlf);

    public sealed record DecodeResult(string Text, EncodingMetadata Metadata);

    static RemoteTextEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>按 BOM（UTF-32 / UTF-8 / UTF-16）→ 严格 UTF-8 → 严格 GB18030 → ISO-8859-1 顺序解码，并在无 BOM 时拒绝含 NUL 的二进制文件。</summary>
    public static DecodeResult Decode(byte[] bytes, int maxBytes = 4 * 1024 * 1024)
    {
        if (bytes.Length > maxBytes)
            throw new InvalidDataException($"文件过大（>{maxBytes / (1024 * 1024)}MB），请先下载");

        string text;
        EncodingMetadata meta;

        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
        {
            text = DecodeBomStrict(
                bytes, 4,
                new UTF32Encoding(bigEndian: false, byteOrderMark: false, throwOnInvalidCharacters: true),
                "UTF-32 LE");
            meta = new EncodingMetadata("UTF-32 LE", 12000, HasBom: true, DetectPureCrlf(text));
            return new DecodeResult(text, meta);
        }

        if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
        {
            text = DecodeBomStrict(
                bytes, 4,
                new UTF32Encoding(bigEndian: true, byteOrderMark: false, throwOnInvalidCharacters: true),
                "UTF-32 BE");
            meta = new EncodingMetadata("UTF-32 BE", 12001, HasBom: true, DetectPureCrlf(text));
            return new DecodeResult(text, meta);
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            text = DecodeBomStrict(
                bytes, 3,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                "UTF-8 BOM");
            meta = new EncodingMetadata("UTF-8 BOM", Encoding.UTF8.CodePage, HasBom: true, DetectPureCrlf(text));
            return new DecodeResult(text, meta);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            text = DecodeBomStrict(
                bytes, 2,
                new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true),
                "UTF-16 LE");
            meta = new EncodingMetadata("UTF-16 LE", 1200, HasBom: true, DetectPureCrlf(text));
            return new DecodeResult(text, meta);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            text = DecodeBomStrict(
                bytes, 2,
                new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true),
                "UTF-16 BE");
            meta = new EncodingMetadata("UTF-16 BE", 1201, HasBom: true, DetectPureCrlf(text));
            return new DecodeResult(text, meta);
        }

        if (Array.IndexOf(bytes, (byte)0) >= 0)
            throw new InvalidDataException("检测到二进制文件（包含 NUL 字节），请使用下载，禁止按文本编辑");

        try
        {
            var utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            text = utf8Strict.GetString(bytes);
            meta = new EncodingMetadata("UTF-8", Encoding.UTF8.CodePage, HasBom: false, DetectPureCrlf(text));
            return new DecodeResult(text, meta);
        }
        catch (DecoderFallbackException)
        {
        }

        try
        {
            var gb18030 = Encoding.GetEncoding(54936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            text = gb18030.GetString(bytes);
            meta = new EncodingMetadata("GB18030", 54936, HasBom: false, DetectPureCrlf(text));
            return new DecodeResult(text, meta);
        }
        catch (DecoderFallbackException)
        {
        }

        var latin1 = Encoding.GetEncoding(28591, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        text = latin1.GetString(bytes);
        meta = new EncodingMetadata("ISO-8859-1", 28591, HasBom: false, DetectPureCrlf(text));
        return new DecodeResult(text, meta);
    }

    /// <summary>按原文件编码与 BOM 严格编码回字节流；不可表示字符明确报错，不静默替换。</summary>
    public static byte[] Encode(string text, EncodingMetadata? metadata)
    {
        var meta = metadata ?? new EncodingMetadata("UTF-8", Encoding.UTF8.CodePage, HasBom: false, PreserveCrlf: false);
        var normalized = meta.PreserveCrlf
            ? text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n")
            : text;

        Encoding encoding = meta.CodePage switch
        {
            65001 => new UTF8Encoding(encoderShouldEmitUTF8Identifier: meta.HasBom, throwOnInvalidBytes: true),
            1200 => new UnicodeEncoding(bigEndian: false, byteOrderMark: meta.HasBom, throwOnInvalidBytes: true),
            1201 => new UnicodeEncoding(bigEndian: true, byteOrderMark: meta.HasBom, throwOnInvalidBytes: true),
            12000 => new UTF32Encoding(bigEndian: false, byteOrderMark: meta.HasBom, throwOnInvalidCharacters: true),
            12001 => new UTF32Encoding(bigEndian: true, byteOrderMark: meta.HasBom, throwOnInvalidCharacters: true),
            _ => Encoding.GetEncoding(meta.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback),
        };

        try
        {
            var preamble = meta.HasBom ? encoding.GetPreamble() : Array.Empty<byte>();
            var payload = encoding.GetBytes(normalized);
            if (preamble.Length == 0) return payload;
            var result = new byte[preamble.Length + payload.Length];
            Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
            Buffer.BlockCopy(payload, 0, result, preamble.Length, payload.Length);
            return result;
        }
        catch (EncoderFallbackException ex)
        {
            throw new InvalidOperationException($"文本包含无法使用原编码 {meta.Name} 表示的字符（{ex.Message}），已阻止保存以免数据损坏", ex);
        }
    }

    private static string DecodeBomStrict(byte[] bytes, int offset, Encoding encoding, string encodingName)
    {
        try
        {
            return encoding.GetString(bytes, offset, bytes.Length - offset);
        }
        catch (Exception ex) when (ex is DecoderFallbackException or ArgumentException)
        {
            throw new InvalidDataException($"文件包含无效 {encodingName} 字节序列或属于二进制文件，请使用下载", ex);
        }
    }

    private static bool DetectPureCrlf(string text)
    {
        if (!text.Contains("\r\n", StringComparison.Ordinal)) return false;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n' && (i == 0 || text[i - 1] != '\r'))
                return false;
        }
        return true;
    }
}
