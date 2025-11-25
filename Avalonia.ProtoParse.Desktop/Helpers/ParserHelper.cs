using System;
using System.IO;
using System.IO.Compression;

namespace Avalonia.ProtoParse.Desktop.Helpers;

public static class ParserHelper
{
    /// <summary>
    /// 处理文本输入（Hex, Base64, Gzip）并返回原始字节数据
    /// </summary>
    public static byte[] ProcessInputText(string input)
    {
        var cleanInput = input.Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "");
        
        if (string.IsNullOrEmpty(cleanInput)) return Array.Empty<byte>();

        byte[]? data = null;

        // 1. Try Hex
        try
        {
            data = Convert.FromHexString(cleanInput);
        }
        catch
        {
            // Hex failed, continue to Base64
        }

        // 2. Try Base64 if Hex failed
        if (data == null)
        {
            try
            {
                // Try Base64 (handle URL-safe chars and padding)
                var base64 = cleanInput.Replace('-', '+').Replace('_', '/');
                switch (base64.Length % 4)
                {
                    case 2: base64 += "=="; break;
                    case 3: base64 += "="; break;
                }
                data = Convert.FromBase64String(base64);
            }
            catch
            {
                // Both failed
                throw new FormatException("无法识别的输入格式 (非 Hex 或 Base64)");
            }
        }

        // 3. Try Gzip Decompression
        return TryDecompress(data);
    }

    /// <summary>
    /// 处理字节输入（主要是检测并解压 Gzip）
    /// </summary>
    public static byte[] ProcessInputBytes(byte[] data)
    {
        if (data == null || data.Length == 0) return Array.Empty<byte>();
        return TryDecompress(data);
    }

    private static byte[] TryDecompress(byte[] data)
    {
        // Check for Gzip Magic Number (0x1F 0x8B)
        if (data.Length > 2 && data[0] == 0x1F && data[1] == 0x8B)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var gzip = new GZipStream(ms, CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                gzip.CopyTo(outMs);
                return outMs.ToArray();
            }
            catch
            {
                // Ignore Gzip errors, might be false positive or corrupted, fallback to original data
                return data;
            }
        }
        return data;
    }
}
