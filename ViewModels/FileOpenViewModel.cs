using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using OpenccNetLibGui.Helpers;
using OpenccNetLibGui.Models;

namespace OpenccNetLibGui.ViewModels;

internal static class FileOpenViewModel
{
    public static async Task<FileOpenResult> OpenAsync(string path)
    {
        try
        {
            if (OpenXmlHelper.IsDocx(path))
            {
                var text = await Task.Run(() => OpenXmlHelper.ExtractDocxAllText(path));
                return new FileOpenResult(path) { Text = text };
            }

            if (OpenXmlHelper.IsOdt(path))
            {
                var text = await Task.Run(() => OpenXmlHelper.ExtractOdtAllText(path));
                return new FileOpenResult(path) { Text = text };
            }

            if (EpubHelper.IsEpub(path))
            {
                var text = await Task.Run(() => EpubHelper.ExtractEpubAllText(path));
                return new FileOpenResult(path) { Text = text };
            }

            return await OpenTextFileAsync(path);
        }
        catch (Exception ex)
        {
            return new FileOpenResult(path) { Error = ex.Message };
        }
    }

    internal static async Task<FileOpenResult> OpenTextFileAsync(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        var detected = CjkEncodingDetector.Detect(bytes);

        var encoding = GetEncoding(detected.Encoding);
        var wasAutoDetected =
            detected.Encoding != CjkEncodingDetector.EncodingKind.Unknown;

        // Unknown deliberately falls back to UTF-8 with replacement
        // characters. The user can then choose an explicit encoding from
        // the filename encoding menu.
        encoding ??= Encoding.UTF8;

        var offset = Math.Min(detected.BomSize, bytes.Length);
        var text = encoding.GetString(bytes, offset, bytes.Length - offset);

        return new FileOpenResult(path)
        {
            Text = text,
            EncodingName = GetEncodingName(detected.Encoding),
            WasAutoDetected = wasAutoDetected
        };
    }

    private static Encoding? GetEncoding(CjkEncodingDetector.EncodingKind kind)
    {
        return kind switch
        {
            CjkEncodingDetector.EncodingKind.Ascii
                or CjkEncodingDetector.EncodingKind.Utf8
                or CjkEncodingDetector.EncodingKind.Utf8Bom
                => Encoding.UTF8,

            CjkEncodingDetector.EncodingKind.Utf16Le
                or CjkEncodingDetector.EncodingKind.Utf16LeBom
                => Encoding.Unicode,

            CjkEncodingDetector.EncodingKind.Utf16Be
                or CjkEncodingDetector.EncodingKind.Utf16BeBom
                => Encoding.BigEndianUnicode,

            CjkEncodingDetector.EncodingKind.Big5
                => Encoding.GetEncoding("Big5"),

            CjkEncodingDetector.EncodingKind.Gb18030
                => Encoding.GetEncoding("GB18030"),

            _ => null
        };
    }

    private static string GetEncodingName(CjkEncodingDetector.EncodingKind kind)
    {
        return kind switch
        {
            CjkEncodingDetector.EncodingKind.Ascii
                or CjkEncodingDetector.EncodingKind.Utf8
                or CjkEncodingDetector.EncodingKind.Utf8Bom
                => "UTF-8",

            CjkEncodingDetector.EncodingKind.Utf16Le
                or CjkEncodingDetector.EncodingKind.Utf16LeBom
                => "UTF-16LE",

            CjkEncodingDetector.EncodingKind.Utf16Be
                or CjkEncodingDetector.EncodingKind.Utf16BeBom
                => "UTF-16BE",

            CjkEncodingDetector.EncodingKind.Big5
                => "Big5",

            CjkEncodingDetector.EncodingKind.Gb18030
                => "GB18030",

            _ => "UTF-8"
        };
    }
}

internal sealed class FileOpenResult
{
    public string Path { get; }
    public string? Text { get; init; }
    public string? Error { get; init; }

    /// <summary>
    /// Actual decoder used for text files. Null for structured document formats.
    /// Unknown detection falls back to UTF-8.
    /// </summary>
    public string? EncodingName { get; init; }

    /// <summary>
    /// True when the text encoding was identified by CjkEncodingDetector.
    /// </summary>
    public bool WasAutoDetected { get; init; }

    public FileOpenResult(string path)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
    }
}

/// <summary>
/// Provides safeguards against decoded text that can cause pathological
/// layout behavior when displayed in AvaloniaEdit.
/// </summary>
internal static class TextDisplaySafety
{
    // Conservative cap to bound AvaloniaEdit's work on a single logical line.
    private const int MaxLogicalLineLength = 10_000;

    /// <summary>
    /// Determines whether the text contains a logical line that is too long
    /// to display safely in AvaloniaEdit.
    /// </summary>
    /// <param name="text">The decoded text to inspect.</param>
    /// <returns>
    /// <see langword="true"/> if a logical line exceeds the display limit;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool HasOversizedLogicalLine(string text)
    {
        var lineLength = 0;

        foreach (var character in text)
        {
            // Resetting on both characters also handles CRLF without counting either.
            if (character is '\r' or '\n')
                lineLength = 0;
            else if (++lineLength > MaxLogicalLineLength)
                return true;
        }

        return false;
    }
}