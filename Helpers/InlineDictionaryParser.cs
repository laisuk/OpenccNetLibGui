using System;
using System.Collections.Generic;
using System.IO;
using OpenccNetLibGui.Services;

namespace OpenccNetLibGui.Helpers;

internal static class InlineDictionaryParser
{
    internal static Dictionary<string, string> Parse(string? text, int rowNumber, DictionaryGeneratorContents contents)
    {
        text ??= string.Empty;
        // File.ReadLines consumes a UTF-8 BOM. Accept one at the start of pasted text too.
        using var reader = new StringReader(text.StartsWith('\uFEFF') ? text[1..] : text);
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            var entry = line.AsSpan().TrimEnd();
            if (!entry.IsEmpty && entry[0] == '#')
                continue;

            // Look for the separator before trimming so a trailing Tab reports an empty target.
            var tabIndex = line.IndexOf('\t');
            if (tabIndex < 0)
            {
                if (entry.IsEmpty)
                    continue;

                throw new FormatException(string.Format(contents.InlineMissingTabFormat, rowNumber, lineNumber));
            }

            if (tabIndex == 0)
                throw new FormatException(string.Format(contents.InlineEmptySourceFormat, rowNumber, lineNumber));
            if (tabIndex >= entry.Length - 1)
                throw new FormatException(string.Format(contents.InlineEmptyTargetFormat, rowNumber, lineNumber));

            // Match Core's first target token, including whitespace before the token.
            var target = entry[(tabIndex + 1)..];
            var targetEnd = 0;
            var seenNonWhitespace = false;
            while (targetEnd < target.Length)
            {
                if (!char.IsWhiteSpace(target[targetEnd]))
                    seenNonWhitespace = true;
                else if (seenNonWhitespace)
                    break;

                targetEnd++;
            }

            pairs[entry[..tabIndex].ToString()] = target[..targetEnd].ToString();
        }

        if (pairs.Count == 0)
            throw new FormatException(string.Format(contents.InlineEmptyDictionaryFormat, rowNumber));

        return pairs;
    }
}
