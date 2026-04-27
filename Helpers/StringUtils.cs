using System;

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
}