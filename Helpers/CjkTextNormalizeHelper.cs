using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("OpenccNetLibGuiTests")]

namespace OpenccNetLibGui.Helpers
{
    internal static class CjkTextNormalizeHelper
    {
        private const char AsciiDouble = '"';
        private const char OpenDouble = '“';
        private const char CloseDouble = '”';

        private const char AsciiSingle = '\'';
        private const char OpenSingle = '‘';
        private const char CloseSingle = '’';

        // Private Use Area marker used only during normalization.
        private const char MaskedLatinSingleQuote = '\uE000';

        /// <summary>
        /// Normalizes dialog quotes in CJK text.
        ///
        /// ASCII single (<c>'</c>) and double (<c>"</c>) quotes are interpreted as
        /// unknown dialog quotes and converted into the corresponding CJK opening
        /// and closing quotation marks while preserving the current quote state.
        ///
        /// Existing CJK quotation marks also update the state, allowing mixed input
        /// such as <c>“hello"</c> or <c>"hello”</c> to be normalized correctly.
        /// </summary>
        internal static string NormalizeCjkTextDialogQuotes(
            string text,
            bool preserveLatinSingleQuotes = true)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            if (preserveLatinSingleQuotes)
                text = MaskLatinSingleQuotes(text);

            var state = new DialogQuoteState();
            var sb = new StringBuilder(text.Length);

            foreach (var ch in text)
            {
                sb.Append(state.NormalizeChar(ch));
            }

            var normalized = sb.ToString();

            if (preserveLatinSingleQuotes)
                normalized = normalized.Replace(MaskedLatinSingleQuote, AsciiSingle);

            return normalized;
        }

        /// <summary>
        /// Masks apostrophes between Latin/letter characters before dialog quote
        /// normalization, so words such as <c>don't</c>, <c>I'm</c>,
        /// <c>rock'n'roll</c>, and <c>O'Brien</c> are preserved.
        /// </summary>
        private static string MaskLatinSingleQuotes(string text)
        {
            var sb = new StringBuilder(text.Length);

            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];

                if (ch == AsciiSingle &&
                    i > 0 &&
                    i + 1 < text.Length &&
                    IsAsciiLetter(text[i - 1]) &&
                    IsAsciiLetter(text[i + 1]))
                {
                    sb.Append(MaskedLatinSingleQuote);
                    continue;
                }

                sb.Append(ch);
            }

            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAsciiLetter(char ch)
        {
            return ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
        }

        private struct DialogQuoteState
        {
            private bool _insideDouble;
            private bool _insideSingle;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal char NormalizeChar(char ch)
            {
                switch (ch)
                {
                    case OpenDouble:
                        _insideDouble = true;
                        return ch;

                    case CloseDouble:
                        _insideDouble = false;
                        return ch;

                    case AsciiDouble:
                        if (_insideDouble)
                        {
                            _insideDouble = false;
                            return CloseDouble;
                        }

                        _insideDouble = true;
                        return OpenDouble;

                    case OpenSingle:
                        _insideSingle = true;
                        return ch;

                    case CloseSingle:
                        _insideSingle = false;
                        return ch;

                    case AsciiSingle:
                        if (_insideSingle)
                        {
                            _insideSingle = false;
                            return CloseSingle;
                        }

                        _insideSingle = true;
                        return OpenSingle;

                    default:
                        return ch;
                }
            }
        }

        public static DialogQuoteValidationResult ValidateDialogQuotes(string text)
        {
            var result = new DialogQuoteValidationResult();

            var lines = text.Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var stripped = line.Trim();

                if (string.IsNullOrEmpty(stripped))
                    continue;

                if ((stripped.StartsWith('”') && stripped.EndsWith('“')) ||
                    (stripped.StartsWith('’') && stripped.EndsWith('‘')))
                {
                    result.SuspiciousLines.Add(new DialogQuoteIssue
                    {
                        LineNumber = i + 1,
                        Text = line
                    });
                }
            }

            return result;
        }
    }

    public sealed class DialogQuoteValidationResult
    {
        public bool IsValid => SuspiciousLines.Count == 0;

        public List<DialogQuoteIssue> SuspiciousLines { get; } = new()
        {
            Capacity = 0
        };

        public string BuildSummary()
        {
            if (IsValid)
                return "No suspicious dialog quote issues found.";

            var sb = new StringBuilder();

            sb.AppendLine($"Found {SuspiciousLines.Count} suspicious dialog quote line(s).");
            sb.AppendLine();
            sb.AppendLine("Hint:");
            sb.AppendLine("The actual typo is often a missing or extra dialog quote");
            sb.AppendLine("a few lines above the first reported line.");
            sb.AppendLine("Fix the source text and validate again.");
            sb.AppendLine();

            foreach (var item in SuspiciousLines.Take(5))
            {
                sb.AppendLine($"{item.LineNumber}: {item.Text}");
            }

            if (SuspiciousLines.Count > 5)
                sb.AppendLine($"...and {SuspiciousLines.Count - 5} more.");

            return sb.ToString();
        }
    }

    public sealed class DialogQuoteIssue
    {
        public int LineNumber { get; init; }

        public string Text { get; init; } = "";
    }
}