using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenccNetLibGui.Helpers;

public static class StringUtils
{
    /// <summary>
    /// Truncate a string in the middle with ellipsis, preserving the tail (e.g., file extension).
    /// </summary>
    /// <param name="input">Input string</param>
    /// <param name="maxLength">Maximum total length</param>
    /// <param name="headLength">Optional fixed head length (auto if null)</param>
    /// <param name="tailLength">Optional fixed tail length (auto if null)</param>
    /// <param name="ellipsis">Ellipsis string (default "...")</param>
    /// <returns>Truncated string</returns>
    public static string MiddleEllipsis(
        string? input,
        int maxLength = 50,
        int? headLength = null,
        int? tailLength = null,
        string ellipsis = "...")
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        if (input.Length <= maxLength)
            return input;

        var ellipsisLen = ellipsis.Length;

        // Auto-calculate head/tail if not provided
        var head = headLength ?? (maxLength - ellipsisLen) / 2;
        var tail = tailLength ?? (maxLength - ellipsisLen - head);

        // Safety clamp
        if (head < 0) head = 0;
        if (tail < 0) tail = 0;

        if (head + tail + ellipsisLen <= maxLength)
            return string.Concat(
                input.AsSpan(0, head),
                ellipsis,
                input.AsSpan(input.Length - tail)
            );
        tail = maxLength - ellipsisLen - head;
        if (tail < 0) tail = 0;

        return string.Concat(
            input.AsSpan(0, head),
            ellipsis,
            input.AsSpan(input.Length - tail)
        );
    }

    // Unicode Compatibility Normalization

    private static readonly Lazy<Dictionary<int, string>>
        UnicodeCompatibilityMap =
            new(LoadUnicodeCompatibilityMap);

    internal static string NormalizeUnicodeCompatibility(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var map = UnicodeCompatibilityMap.Value;
        StringBuilder? sb = null;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            int codePoint = ch;
            var charCount = 1;

            if (char.IsHighSurrogate(ch) &&
                i + 1 < text.Length &&
                char.IsLowSurrogate(text[i + 1]))
            {
                codePoint = char.ConvertToUtf32(ch, text[i + 1]);
                charCount = 2;
            }

            if (!map.TryGetValue(codePoint, out var replacement))
            {
                if (sb is not null)
                {
                    sb.Append(ch);
                    if (charCount == 2)
                        sb.Append(text[++i]);
                }

                continue;
            }

            if (sb is null)
            {
                sb = new StringBuilder(text.Length);
                sb.Append(text, 0, i);
            }

            sb.Append(replacement);

            if (charCount == 2)
                i++;
        }

        return sb?.ToString() ?? text;
    }

    private static Dictionary<int, string>
        LoadUnicodeCompatibilityMap()
    {
        var map = new Dictionary<int, string>(256);

        var path = Path.Combine(
            AppContext.BaseDirectory,
            "dicts",
            "Unicode_Compatibility.txt");

        if (!File.Exists(path))
            return map;

        foreach (var rawLine in File.ReadLines(path, Encoding.UTF8))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line[0] == '#')
                continue;

            var parts = line.Split('\t');

            if (parts.Length != 2 ||
                parts[0].Length == 0 ||
                parts[1].Length == 0)
                continue;

            var source = parts[0];
            var codePoint = char.ConvertToUtf32(source, 0);
            var charCount = char.IsSurrogatePair(source, 0) ? 2 : 1;

            if (source.Length != charCount)
                continue;

            map[codePoint] = parts[1];
        }

        return map;
    }
}