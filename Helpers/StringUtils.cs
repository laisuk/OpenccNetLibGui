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

    private const char CompatibilityMin = '\u2300';
    private const char CompatibilityMax = '\u2FFF';

    private static readonly Lazy<Dictionary<char, string>>
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

            if (ch < CompatibilityMin || ch > CompatibilityMax ||
                !map.TryGetValue(ch, out var replacement))
            {
                sb?.Append(ch);
                continue;
            }

            if (sb is null)
            {
                sb = new StringBuilder(text.Length);
                sb.Append(text, 0, i);
            }

            sb.Append(replacement);
        }

        return sb?.ToString() ?? text;
    }

    private static Dictionary<char, string>
        LoadUnicodeCompatibilityMap()
    {
        var map = new Dictionary<char, string>(256);

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
                parts[0].Length != 1 ||
                parts[1].Length == 0)
                continue;

            map[parts[0][0]] = parts[1];
        }

        return map;
    }
}