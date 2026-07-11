using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("OpenccNetLibGuiTests")]

namespace OpenccNetLibGui.Helpers
{
    /// <summary>
    /// Provides helper methods for normalizing and validating quotation marks
    /// commonly found in CJK text.
    /// </summary>
    internal static class CjkTextNormalizeHelper
    {
        private const char AsciiDouble = '"';
        private const char OpenDouble = '“';
        private const char CloseDouble = '”';

        private const char AsciiSingle = '\'';
        private const char OpenSingle = '‘';
        private const char CloseSingle = '’';

        // Traditional Chinese outer dialog quote: 「」
        private const char OpenCornerDouble = '「';
        private const char CloseCornerDouble = '」';

        // Traditional Chinese inner dialog quote: 『』
        private const char OpenCornerSingle = '『';
        private const char CloseCornerSingle = '』';

        // Private Use Area marker used only during normalization.
        private const char MaskedLatinSingleQuote = '\uE000';

        /// <summary>
        /// Normalizes ASCII dialog quotation marks in CJK text.
        /// </summary>
        /// <param name="text">
        /// The input text to normalize.
        /// </param>
        /// <param name="preserveLatinSingleQuotes">
        /// <see langword="true"/> to preserve ASCII apostrophes between Latin
        /// letters, such as those in <c>don't</c>, <c>I'm</c>,
        /// <c>rock'n'roll</c>, and <c>O'Brien</c>;
        /// otherwise, every ASCII single quote is interpreted as a dialog quote.
        /// </param>
        /// <returns>
        /// The normalized text.
        /// </returns>
        /// <remarks>
        /// <para>
        /// ASCII double and single quotes are interpreted as unknown dialog
        /// quotation marks and converted according to the current quotation state.
        /// </para>
        ///
        /// <para>
        /// Existing curly quotation marks update the state:
        /// </para>
        ///
        /// <code>
        /// “dialog”
        /// ‘nested dialog’
        /// </code>
        ///
        /// <para>
        /// Existing Traditional Chinese corner quotation marks also update the
        /// state:
        /// </para>
        ///
        /// <code>
        /// 「對話」
        /// 『內層對話』
        /// </code>
        ///
        /// <para>
        /// When an ASCII quote closes an existing quotation, the currently active
        /// quote family is preserved. For example:
        /// </para>
        ///
        /// <code>
        /// “Hello"  becomes  “Hello”
        /// 「Hello"  becomes  「Hello」
        /// ‘Hello'  becomes  ‘Hello’
        /// 『Hello'  becomes  『Hello』
        /// </code>
        ///
        /// <para>
        /// When an ASCII quote opens a new quotation and no existing quote family
        /// is active, curly quotation marks are used by default.
        /// </para>
        /// </remarks>
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
            {
                normalized = normalized.Replace(
                    MaskedLatinSingleQuote,
                    AsciiSingle);
            }

            return normalized;
        }

        /// <summary>
        /// Validates completed dialog quote pairs that appear at the beginning
        /// and end of individual text lines.
        /// </summary>
        /// <param name="text">
        /// The text whose dialog quotation marks should be inspected.
        /// </param>
        /// <returns>
        /// A validation result containing every suspicious line found.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This validator identifies two common categories of malformed dialog
        /// quotation marks:
        /// </para>
        ///
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// Reversed pairs, such as <c>”Hello“</c> or <c>」你好「</c>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Mixed quote families or levels, such as <c>「Hello”</c>,
        /// <c>“Hello」</c>, or <c>「Hello』</c>.
        /// </description>
        /// </item>
        /// </list>
        ///
        /// <para>
        /// Mixed quotation styles are not automatically corrected because the
        /// intended style is ambiguous. The caller can present the suspicious
        /// line to the user and allow the user to choose the desired quotation
        /// pair manually.
        /// </para>
        ///
        /// <para>
        /// The validator deliberately checks only lines whose first and last
        /// non-whitespace characters are quotation marks. It does not attempt to
        /// perform full multi-line quote balancing.
        /// </para>
        /// </remarks>
        public static DialogQuoteValidationResult ValidateDialogQuotes(
            string text)
        {
            var result = new DialogQuoteValidationResult();

            if (string.IsNullOrEmpty(text))
                return result;

            var lines = text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var stripped = line.Trim();

                if (string.IsNullOrEmpty(stripped))
                    continue;

                if (!HasSuspiciousDialogQuotePair(stripped))
                    continue;

                result.SuspiciousLines.Add(new DialogQuoteIssue
                {
                    LineNumber = i + 1,
                    Text = line
                });
            }

            return result;
        }

        /// <summary>
        /// Masks ASCII apostrophes that appear between Latin letters.
        /// </summary>
        /// <param name="text">
        /// The text whose Latin apostrophes should be protected.
        /// </param>
        /// <returns>
        /// A temporary string in which protected apostrophes have been replaced
        /// with a private-use marker.
        /// </returns>
        /// <remarks>
        /// This prevents apostrophes in words such as <c>don't</c>,
        /// <c>I'm</c>, <c>rock'n'roll</c>, and <c>O'Brien</c> from being
        /// interpreted as dialog quotation marks.
        /// </remarks>
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

        /// <summary>
        /// Determines whether a character is an ASCII Latin letter.
        /// </summary>
        /// <param name="ch">
        /// The character to inspect.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when <paramref name="ch"/> is in the range
        /// <c>A-Z</c> or <c>a-z</c>; otherwise, <see langword="false"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAsciiLetter(char ch)
        {
            return ch is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z';
        }

        /// <summary>
        /// Determines whether a line begins and ends with a suspicious dialog
        /// quotation-mark combination.
        /// </summary>
        /// <param name="stripped">
        /// A non-empty line with leading and trailing whitespace removed.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when the line contains a reversed or mismatched
        /// completed quote pair; otherwise, <see langword="false"/>.
        /// </returns>
        private static bool HasSuspiciousDialogQuotePair(string stripped)
        {
            if (stripped.Length < 2)
                return false;

            var first = stripped[0];
            var last = stripped[^1];

            // Reversed pair:
            // ”...“ / ’...‘ / 」...「 / 』...『
            if (IsDialogQuoteCloser(first) &&
                IsDialogQuoteOpener(last))
            {
                return true;
            }

            // Both ends are quote marks, but they do not form a valid pair:
            // 「...” / “...」 / 「...』 / 『...」, etc.
            return IsDialogQuoteOpener(first) &&
                   IsDialogQuoteCloser(last) &&
                   !IsMatchingDialogQuotePair(first, last);
        }

        /// <summary>
        /// Determines whether a character is a supported opening dialog
        /// quotation mark.
        /// </summary>
        /// <param name="ch">
        /// The character to inspect.
        /// </param>
        /// <returns>
        /// <see langword="true"/> for <c>“</c>, <c>‘</c>, <c>「</c>,
        /// or <c>『</c>; otherwise, <see langword="false"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsDialogQuoteOpener(char ch)
        {
            return ch is OpenDouble
                or OpenSingle
                or OpenCornerDouble
                or OpenCornerSingle;
        }

        /// <summary>
        /// Determines whether a character is a supported closing dialog
        /// quotation mark.
        /// </summary>
        /// <param name="ch">
        /// The character to inspect.
        /// </param>
        /// <returns>
        /// <see langword="true"/> for <c>”</c>, <c>’</c>, <c>」</c>,
        /// or <c>』</c>; otherwise, <see langword="false"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsDialogQuoteCloser(char ch)
        {
            return ch is CloseDouble
                or CloseSingle
                or CloseCornerDouble
                or CloseCornerSingle;
        }

        /// <summary>
        /// Determines whether an opening quotation mark and a closing quotation
        /// mark form one supported matching pair.
        /// </summary>
        /// <param name="open">
        /// The opening quotation mark.
        /// </param>
        /// <param name="close">
        /// The closing quotation mark.
        /// </param>
        /// <returns>
        /// <see langword="true"/> for the pairs <c>“”</c>, <c>‘’</c>,
        /// <c>「」</c>, and <c>『』</c>; otherwise,
        /// <see langword="false"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsMatchingDialogQuotePair(
            char open,
            char close)
        {
            return
                (open == OpenDouble &&
                 close == CloseDouble) ||
                (open == OpenSingle &&
                 close == CloseSingle) ||
                (open == OpenCornerDouble &&
                 close == CloseCornerDouble) ||
                (open == OpenCornerSingle &&
                 close == CloseCornerSingle);
        }

        /// <summary>
        /// Represents the quotation-mark family currently active for one logical
        /// dialog quote level.
        /// </summary>
        private enum DialogQuoteFamily : byte
        {
            /// <summary>
            /// No quotation mark is currently open.
            /// </summary>
            None,

            /// <summary>
            /// Curly quotation marks are currently active:
            /// <c>“”</c> or <c>‘’</c>.
            /// </summary>
            Curly,

            /// <summary>
            /// Traditional Chinese corner quotation marks are currently active:
            /// <c>「」</c> or <c>『』</c>.
            /// </summary>
            Corner
        }

        /// <summary>
        /// Tracks the active outer and inner dialog quotation-mark families while
        /// text is normalized.
        /// </summary>
        private struct DialogQuoteState
        {
            private DialogQuoteFamily _doubleFamily;
            private DialogQuoteFamily _singleFamily;

            /// <summary>
            /// Normalizes one character and updates the current quotation state.
            /// </summary>
            /// <param name="ch">
            /// The source character.
            /// </param>
            /// <returns>
            /// The normalized character.
            /// </returns>
            /// <remarks>
            /// Existing CJK quotation marks are preserved exactly. They only
            /// update the state used to interpret subsequent ASCII quotation
            /// marks.
            /// </remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal char NormalizeChar(char ch)
            {
                switch (ch)
                {
                    case OpenDouble:
                        _doubleFamily = DialogQuoteFamily.Curly;
                        return ch;

                    case CloseDouble:
                        _doubleFamily = DialogQuoteFamily.None;
                        return ch;

                    case OpenCornerDouble:
                        _doubleFamily = DialogQuoteFamily.Corner;
                        return ch;

                    case CloseCornerDouble:
                        _doubleFamily = DialogQuoteFamily.None;
                        return ch;

                    case AsciiDouble:
                        return NormalizeAsciiDouble();

                    case OpenSingle:
                        _singleFamily = DialogQuoteFamily.Curly;
                        return ch;

                    case CloseSingle:
                        _singleFamily = DialogQuoteFamily.None;
                        return ch;

                    case OpenCornerSingle:
                        _singleFamily = DialogQuoteFamily.Corner;
                        return ch;

                    case CloseCornerSingle:
                        _singleFamily = DialogQuoteFamily.None;
                        return ch;

                    case AsciiSingle:
                        return NormalizeAsciiSingle();

                    default:
                        return ch;
                }
            }

            /// <summary>
            /// Normalizes one ASCII double quote according to the active outer
            /// dialog quote family.
            /// </summary>
            /// <returns>
            /// A matching closing quote when an outer quotation is already open;
            /// otherwise, a curly opening double quote.
            /// </returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private char NormalizeAsciiDouble()
            {
                switch (_doubleFamily)
                {
                    case DialogQuoteFamily.Curly:
                        _doubleFamily = DialogQuoteFamily.None;
                        return CloseDouble;

                    case DialogQuoteFamily.Corner:
                        _doubleFamily = DialogQuoteFamily.None;
                        return CloseCornerDouble;

                    default:
                        _doubleFamily = DialogQuoteFamily.Curly;
                        return OpenDouble;
                }
            }

            /// <summary>
            /// Normalizes one ASCII single quote according to the active inner
            /// dialog quote family.
            /// </summary>
            /// <returns>
            /// A matching closing quote when an inner quotation is already open;
            /// otherwise, a curly opening single quote.
            /// </returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private char NormalizeAsciiSingle()
            {
                switch (_singleFamily)
                {
                    case DialogQuoteFamily.Curly:
                        _singleFamily = DialogQuoteFamily.None;
                        return CloseSingle;

                    case DialogQuoteFamily.Corner:
                        _singleFamily = DialogQuoteFamily.None;
                        return CloseCornerSingle;

                    default:
                        _singleFamily = DialogQuoteFamily.Curly;
                        return OpenSingle;
                }
            }
        }
    }

    /// <summary>
    /// Contains the result of dialog quotation-mark validation.
    /// </summary>
    public sealed class DialogQuoteValidationResult
    {
        /// <summary>
        /// Gets a value indicating whether no suspicious dialog quote lines were
        /// found.
        /// </summary>
        public bool IsValid => SuspiciousLines.Count == 0;

        /// <summary>
        /// Gets the suspicious dialog quote lines found during validation.
        /// </summary>
        public List<DialogQuoteIssue> SuspiciousLines { get; } = new()
        {
            Capacity = 0
        };

        /// <summary>
        /// Builds a user-facing summary of the validation result.
        /// </summary>
        /// <returns>
        /// A short validation summary containing up to the first five suspicious
        /// lines.
        /// </returns>
        public string BuildSummary()
        {
            if (IsValid)
                return "No suspicious dialog quote issues found.";

            var sb = new StringBuilder();

            sb.AppendLine(
                $"Found {SuspiciousLines.Count} suspicious dialog quote line(s).");

            sb.AppendLine();
            sb.AppendLine("Hint:");
            sb.AppendLine(
                "The actual typo is often a missing, extra, reversed, or mixed dialog quote");
            sb.AppendLine(
                "on the reported line or a few lines above it.");
            sb.AppendLine(
                "Fix the source text and validate again.");
            sb.AppendLine();

            foreach (var item in SuspiciousLines.Take(5))
            {
                sb.AppendLine(
                    $"{item.LineNumber}: {item.Text}");
            }

            if (SuspiciousLines.Count > 5)
            {
                sb.AppendLine(
                    $"...and {SuspiciousLines.Count - 5} more.");
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Describes one suspicious dialog quotation-mark line.
    /// </summary>
    public sealed class DialogQuoteIssue
    {
        /// <summary>
        /// Gets the one-based source line number.
        /// </summary>
        public int LineNumber { get; init; }

        /// <summary>
        /// Gets the original source line, including its original indentation.
        /// </summary>
        public string Text { get; init; } = string.Empty;
    }
}