using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using OpenccNetLibGui.Services; // ShortHeadingSettings

namespace OpenccNetLibGui.Models
{
    /// <summary>
    /// Core CJK paragraph reflow engine.
    /// Shared by PdfPig, Pdfium, Office, EPUB, and plain-text pipelines.
    /// </summary>
    internal static class ReflowModel
    {
        // =========================================================
        //  Configuration / constants
        // =========================================================

        // CJK-aware punctuation set (used for paragraph detection)
        private static readonly char[] CjkPunctEndChars =
        {
            // Standard CJK sentence-ending punctuation
            '。', '！', '？', '；', '：', '…', '—', '”', '」', '’', '』', '.',

            // Chinese closing brackets / quotes
            '）', '】', '》', '〗', '〕', '〉', '」', '』', '］', '｝', ')', ':', '!'
        };

        // Chapter / heading patterns (短行 + 第N章/卷/节/部, 前言/序章/终章/尾声/番外)
        private static readonly Regex TitleHeadingRegex =
            new(
                @"^(?=.{0,60}$)
                  (前言|序章|终章|尾声|后记|尾聲|後記|番外.{0,10}
                  |.{0,20}?第.{0,10}?([章节部卷節回][^分合]).{0,20}?
                  )",
                RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

        //Paragraph indentation
        private static readonly Regex IndentRegex =
            new(@"^[\s\u3000]{2,}", RegexOptions.Compiled);

        // Dialog brackets (Simplified / Traditional / JP-style)
        private const string DialogOpeners = "“‘「『﹁﹃";

        private static bool IsDialogOpener(char ch)
            => DialogOpeners.Contains(ch);

        // Bracket punctuations (open-close)
        private const string OpenBrackets = "（([【《｛〈";
        private const string CloseBrackets = "）)]】》｝〉";

        // Metadata key-value separators
        private static readonly char[] MetadataSeparators =
        {
            '：', // full-width colon
            ':', // ASCII colon
            '　', // full-width ideographic space (U+3000)
            '・' // full-width ideographic dot (U+3000)
        };

        // Metadata heading title noames
        private static readonly HashSet<string> MetadataKeys = new(StringComparer.Ordinal)
        {
            // ===== 1. Title / Author / Publishing =====
            "書名", "书名",
            "作者",
            "原著",
            "譯者", "译者",
            "校訂", "校订",
            "出版社",
            "出版時間", "出版时间",
            "出版日期",

            // ===== 2. Copyright / License =====
            "版權", "版权",
            "版權頁", "版权页",
            "版權信息", "版权信息",

            // ===== 3. Editor / Pricing =====
            "責任編輯", "责任编辑",
            "編輯", "编辑", // 有些出版社簡化成「编辑」
            "責編", "责编", // 等同责任编辑，但常見
            "定價", "定价",

            // ===== 4. Descriptions / Forewords =====
            // "內容簡介", "内容简介",
            // "作者簡介", "作者简介",
            "簡介", "简介",
            "前言",
            "序章",
            "終章", "终章",
            "尾聲", "尾声",
            "後記", "后记",

            // ===== 5. Digital Publishing (ebook platforms) =====
            "品牌方",
            "出品方",
            "授權方", "授权方",
            "電子版權", "数字版权",
            "掃描", "扫描",
            "發行", "发行",
            "OCR",

            // ===== 6. CIP / Cataloging =====
            "CIP",
            "在版編目", "在版编目",
            "分類號", "分类号",
            "主題詞", "主题词",
            "類型", "类型",
            "標簽", "标签",
            "系列",

            // ===== 7. Publishing Cycle =====
            "發行日", "发行日",
            "初版",

            // ===== 8. Common keys without variants =====
            "ISBN"
        };

        // =========================================================
        //  Public entry point
        // =========================================================

        /// <summary>
        /// Reflows CJK (Chinese/Japanese/Korean) text extracted from a PDF into clean,
        /// human-readable paragraphs.
        ///
        /// <para>
        /// PDF text extraction often produces broken lines, incorrect paragraph boundaries,
        /// missing or excessive newlines, and split words across lines or pages.
        /// This method applies a rule-driven reflow pipeline that reconstructs paragraphs
        /// while preserving semantic structure such as titles, headings, dialogs,
        /// metadata blocks, and page markers.
        /// </para>
        /// </summary>
        ///
        /// <param name="text">
        /// Raw text extracted from a PDF (via PdfPig, Pdfium, or any other engine).
        /// The input is expected to be line-based with newline separators.
        /// </param>
        ///
        /// <param name="addPdfPageHeader">
        /// If <c>true</c>, PDF page headers of the form <c>"=== [Page X/Y] ==="</c>
        /// are preserved during reflow.  
        /// If <c>false</c>, page markers (including markers inserted during extraction)
        /// are removed during reconstruction.
        /// </param>
        ///
        /// <param name="compact">
        /// Determines output formatting style:
        /// <list type="bullet">
        ///   <item>
        ///     <description>
        ///     <c>true</c> — Compact mode:  
        ///     Produces one line per paragraph with no blank lines in between.
        ///     Ideal for dictionary building, NLP preprocessing, and plain text exports.
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <description>
        ///     <c>false</c> — Novel mode:  
        ///     Inserts a blank line between paragraphs, matching book-style formatting.
        ///     </description>
        ///   </item>
        /// </list>
        /// </param>
        ///
        /// <param name="shortHeading">
        /// Configuration object that controls how a line is classified as a
        /// <em>short heading</em> during CJK paragraph reflow.
        ///
        /// <para>
        /// The classification is based on a combination of:
        /// </para>
        /// <list type="bullet">
        ///   <item>
        ///     <description>
        ///     <b>Maximum length</b> (<see cref="ShortHeadingSettings.MaxLen"/>):
        ///     Lines longer than this value are never considered headings.
        ///     Typical range is 5–15 characters; default is 8.
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <description>
        ///     <b>Allowed character patterns</b>, such as:
        ///     all CJK characters, all ASCII characters, ASCII digits only,
        ///     or mixed CJK + ASCII (controlled by the corresponding flags
        ///     in <see cref="ShortHeadingSettings"/>).
        ///     </description>
        ///   </item>
        /// </list>
        ///
        /// <para>
        /// Before pattern matching, several <b>absolute rejection rules</b> are applied:
        /// lines containing sentence-ending punctuation, commas or list separators,
        /// unclosed brackets, or PDF page markers are never treated as headings,
        /// even if they satisfy length and pattern constraints.
        /// </para>
        ///
        /// <para>
        /// This rule-based approach avoids hard-coded language assumptions and allows
        /// users to fine-tune heading detection behavior for different document styles,
        /// including novels, technical documents, and bilingual (CJK + English) texts.
        /// </para>
        /// </param>
        ///
        /// <returns>
        /// A fully reflowed, cleanly segmented text string with consistent paragraph breaks,
        /// preserved headings, correctly grouped dialog blocks, and normalized whitespace.
        /// </returns>
        ///
        /// <remarks>
        /// <para>
        /// The reflow engine performs several processing stages:
        /// </para>
        /// <list type="number">
        ///   <item>
        ///     <description><b>Page marker detection</b>  
        ///     Identifies lines representing page headers or separators.
        ///     </description>
        ///   </item>
        ///
        ///   <item>
        ///     <description><b>Metadata block handling</b>  
        ///     Recognizes copyright/ISBN/publishing information and keeps them intact.
        ///     </description>
        ///   </item>
        ///
        ///   <item>
        ///     <description><b>Heading detection</b>  
        ///     Includes:
        ///     <list type="bullet">
        ///       <item><description>Regex-based title/section headings (“第X章”, “序章”, “终章”).</description></item>
        ///       <item><description>Short-heading rules based on configurable length.</description></item>
        ///       <item><description>
        ///       Smart ASCII expansion — English headings automatically allow longer
        ///       lengths to avoid misclassification.
        ///       </description></item>
        ///     </list>
        ///     </description>
        ///   </item>
        ///
        ///   <item>
        ///     <description><b>Dialog grouping</b>  
        ///     Tracks brackets (“「」”, “『』”, '“”', etc.) to keep dialog paragraphs together.
        ///     </description>
        ///   </item>
        ///
        ///   <item>
        ///     <description><b>Paragraph join/reject heuristics</b>  
        ///     Uses punctuation, indentation, heading signals, CJK rules, and colon-continuation
        ///     logic to determine whether a line should join the previous paragraph or start a new one.
        ///     </description>
        ///   </item>
        ///
        ///   <item>
        ///     <description><b>Output formatting</b>  
        ///     Normalizes whitespace, enforces compact or novel layout, removes or preserves
        ///     PDF page markers, and ensures consistent paragraph boundaries.
        ///     </description>
        ///   </item>
        /// </list>
        ///
        /// <para>
        /// This reflow pipeline is designed specifically for CJK text but also handles
        /// mixed CJK/Latin PDFs reliably.  
        /// </para>
        /// </remarks>
        internal static string ReflowCjkParagraphs(
            string text,
            bool addPdfPageHeader,
            bool compact = false,
            ShortHeadingSettings? shortHeading = null)
        {
            shortHeading ??= ShortHeadingSettings.Default;

            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Normalize \r\n and \r into \n for cross-platform stability
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            var lines = text.Split('\n');
            var segments = new List<string>();
            var buffer = new StringBuilder();
            var dialogState = new DialogState();

            foreach (var rawLine in lines)
            {
                // 1) Visual form: keep full-width indent, drop half-width indent on the left, trim only right side
                var stripped = rawLine.TrimEnd();
                stripped = StripHalfWidthIndentKeepFullWidth(stripped);

                // 🔹 NEW: collapse style-layer repeated segments *before* heading detection
                stripped = CollapseRepeatedSegments(stripped);

                // 2) Logical form for heading detection: no indent at all
                var headingProbe = stripped.TrimStart(' ', '\u3000');

                var isTitleHeading = TitleHeadingRegex.IsMatch(headingProbe);
                var isShortHeading = IsHeadingLike(stripped, shortHeading);
                var isMetadata = IsMetadataLine(stripped); // 〈── 新增

                // Collapse style-layer repeated titles
                // if (isTitleHeading)
                // stripped = CollapseRepeatedSegments(stripped);

                // 1) Empty line
                if (stripped.Length == 0)
                {
                    if (!addPdfPageHeader && buffer.Length > 0)
                    {
                        var lastChar = buffer[^1];

                        // Page-break-like blank line, skip it
                        if (Array.IndexOf(CjkPunctEndChars, lastChar) < 0)
                            continue;
                    }

                    // End of paragraph → flush buffer, do not add ""
                    if (buffer.Length > 0)
                    {
                        segments.Add(buffer.ToString());
                        buffer.Clear();
                        dialogState.Reset();
                    }

                    continue;
                }

                // 2) Page markers
                if (stripped.StartsWith("=== ") && stripped.EndsWith("==="))
                {
                    if (buffer.Length > 0)
                    {
                        segments.Add(buffer.ToString());
                        buffer.Clear();
                        dialogState.Reset();
                    }

                    segments.Add(stripped);
                    continue;
                }

                // 3) Titles
                if (isTitleHeading)
                {
                    if (buffer.Length > 0)
                    {
                        segments.Add(buffer.ToString());
                        buffer.Clear();
                        dialogState.Reset();
                    }

                    segments.Add(stripped);
                    continue;
                }

                // 3b) Metadata 行（短 key:val，如「書名：xxx」「作者：yyy」）
                if (isMetadata)
                {
                    if (buffer.Length > 0)
                    {
                        segments.Add(buffer.ToString());
                        buffer.Clear();
                        dialogState.Reset();
                    }

                    // Metadata 每行獨立存放（之後你可以決定係 skip、折疊、顯示）
                    segments.Add(stripped);
                    continue;
                }

                // 3c) 弱 heading-like：只在「上一段安全」且「上一段尾部像一句話的結束」時才生效
                if (isShortHeading)
                {
                    // 判斷當前行是否「全 CJK」（忽略空白）
                    var isAllCjk = true;
                    foreach (var ch in stripped)
                    {
                        if (char.IsWhiteSpace(ch))
                            continue;

                        if (ch > 0x7F) continue;
                        isAllCjk = false;
                        break;
                    }

                    if (buffer.Length > 0)
                    {
                        var bufText = buffer.ToString();

                        // 🔐 1) 若上一段仍有未配對括號／書名號 → 必定是續行，不能當 heading
                        if (HasUnclosedBracket(bufText))
                        {
                            // fall through → 當普通行，由後面的 merge 邏輯處理
                        }
                        else
                        {
                            var bt = bufText.TrimEnd();
                            if (bt.Length > 0)
                            {
                                var last = bt[^1];

                                // 🔸 2) 上一行逗號結尾 → 視作續句，不當 heading
                                if (last is '，' or ',')
                                {
                                    // fall through → default merge
                                }
                                // 🔸 3) 對於「全 CJK 的短 heading-like」，
                                //     如果上一行 *不是* 以 CJK 句末符號結束，也當續句，不切段。
                                else if (isAllCjk && Array.IndexOf(CjkPunctEndChars, last) < 0)
                                {
                                    // e.g.:
                                    //   内容简介： 《盗
                                    //   墓笔记:吴邪的盗墓笔   ← 雖然像短 heading，但上一行未「句號收尾」
                                    // fall through → 當續行
                                }
                                else
                                {
                                    // ✅ 真 heading-like → flush 舊段，再把當前行當作獨立 heading
                                    segments.Add(bufText);
                                    buffer.Clear();
                                    dialogState.Reset();
                                    segments.Add(stripped);
                                    continue;
                                }
                            }
                            else
                            {
                                // buffer 有長度但全空白，其實等同無 → 直接當 heading
                                segments.Add(stripped);
                                continue;
                            }
                        }
                    }
                    else
                    {
                        // buffer 空（文件開頭／上一段剛 flush 完）→ 允許短 heading 單獨出現
                        segments.Add(stripped);
                        continue;
                    }
                }

                // *** DIALOG: treat any line that *starts* with a dialog opener as a new paragraph
                var currentIsDialogStart = IsDialogStarter(stripped);

                if (buffer.Length == 0)
                {
                    // 4) First line inside buffer → start of a new paragraph
                    buffer.Append(stripped);
                    dialogState.Reset();
                    dialogState.Update(stripped);
                    continue;
                }

                // We already have some text in buffer
                var bufferText = buffer.ToString();

                // 🔸 NEW RULE: If previous line ends with comma, 
                //     do NOT flush even if this line starts dialog.
                //     (comma-ending means the sentence is not finished)
                if (bufferText.Length > 0)
                {
                    var trimmed = bufferText.TrimEnd();
                    var last = trimmed.Length > 0 ? trimmed[^1] : '\0';
                    if (last is '，' or ',')
                    {
                        // fall through → treat as continuation
                        // do NOT flush here
                    }
                    else if (currentIsDialogStart)
                    {
                        // *** DIALOG: if this line starts a dialog, 
                        //     flush previous paragraph (only if safe)
                        segments.Add(bufferText);
                        buffer.Clear();
                        buffer.Append(stripped);
                        dialogState.Reset();
                        dialogState.Update(stripped);
                        continue;
                    }
                }
                else
                {
                    // buffer empty, just add new dialog line
                    if (currentIsDialogStart)
                    {
                        buffer.Append(stripped);
                        dialogState.Reset();
                        dialogState.Update(stripped);
                        continue;
                    }
                }


                // NEW RULE: colon + dialog continuation
                // e.g. "她寫了一行字：" + "「如果連自己都不相信……」"
                if (bufferText.EndsWith('：') || bufferText.EndsWith(':'))
                {
                    if (stripped.Length > 0 && DialogOpeners.Contains(stripped[0]))
                    {
                        buffer.Append(stripped);
                        dialogState.Update(stripped);
                        continue;
                    }
                }

                // NOTE: we *do* block splits when dialogState.IsUnclosed,
                // so multi-line dialog stays together. Once all quotes are
                // closed, CJK punctuation may end the paragraph as usual.

                // 5) Ends with CJK punctuation → new paragraph
                if (Array.IndexOf(CjkPunctEndChars, bufferText[^1]) >= 0 &&
                    !dialogState.IsUnclosed)
                {
                    segments.Add(bufferText);
                    buffer.Clear();
                    buffer.Append(stripped);
                    dialogState.Reset();
                    dialogState.Update(stripped);
                    continue;
                }

                // 7) Indentation → new paragraph
                if (IndentRegex.IsMatch(rawLine))
                {
                    segments.Add(bufferText);
                    buffer.Clear();
                    buffer.Append(stripped);
                    dialogState.Reset();
                    dialogState.Update(stripped);
                    continue;
                }

                // 8) Chapter-like endings: 章 / 节 / 部 / 卷 (with trailing brackets)
                if (bufferText.Length <= 12 &&
                    Regex.IsMatch(bufferText, @"(章|节|部|卷|節|回)[】》〗〕〉」』）]*$"))
                {
                    segments.Add(bufferText);
                    buffer.Clear();
                    buffer.Append(stripped);
                    dialogState.Reset();
                    dialogState.Update(stripped);
                    continue;
                }

                // 9) Default merge (soft line break)
                buffer.Append(stripped);
                dialogState.Update(stripped);
            }

            // flush the final buffer
            if (buffer.Length > 0)
                segments.Add(buffer.ToString());

            // Formatting:
            // compact → "p1\np2\np3"
            // novel   → "p1\n\np2\n\np3"
            return compact
                ? string.Join("\n", segments)
                : string.Join("\n\n", segments);


            // ====== Inline helpers ======

            // Helper: does this line start with a dialog opener? (full-width quotes)
            static bool IsDialogStarter(string s)
            {
                s = s.TrimStart(' ', '\u3000'); // ignore indent
                return s.Length > 0 && DialogOpeners.Contains(s[0]);
            }

            static bool IsHeadingLike(string? s, ShortHeadingSettings sh)
            {
                if (s is null)
                    return false;

                s = s.Trim();
                if (s.Length == 0)
                    return false;

                // keep page markers intact
                if (s.StartsWith("=== ") && s.EndsWith("==="))
                    return false;

                // If ends with CJK punctuation → not heading
                var last = s[^1];
                if (Array.IndexOf(CjkPunctEndChars, last) >= 0)
                    return false;

                // Reject headings with unclosed brackets
                if (HasUnclosedBracket(s))
                    return false;

                // Reject any short line containing comma-like separators
                if (s.Contains('，') || s.Contains(',') || s.Contains('、'))
                    return false;

                // Clamp maxLen
                var baseMax = Math.Clamp(sh.MaxLen, 3, 30);
                var len = s.Length;

                // ASCII headings can be longer
                var effectiveMax = baseMax;

                if (sh.AllAsciiEnabled && IsAllAscii(s))
                {
                    effectiveMax = Math.Clamp(baseMax * 2, 10, 30);
                }

                if (len > effectiveMax)
                    return false;

                // Reject any CJK end punctuation inside the string (strong heuristic)
                foreach (var p in CjkPunctEndChars)
                {
                    if (s.Contains(p))
                        return false;
                }

                // ---- Pattern checks (your requested style) ----
                return (sh.AllAsciiEnabled && IsAllAscii(s))
                       || (sh.AllCjkEnabled && IsAllCjk(s))
                       || (sh.AllAsciiDigitsEnabled && IsAllAsciiDigits(s))
                       || (sh.MixedCjkAsciiEnabled && IsMixedCjkAscii(s));
            }

            static bool IsMetadataLine(string line)
            {
                if (string.IsNullOrWhiteSpace(line))
                    return false;

                // A) length limit
                if (line.Length > 30)
                    return false;

                // B) find first separator
                var idx = line.IndexOfAny(MetadataSeparators);
                if (idx is <= 0 or > 10)
                    return false;

                // C) extract key
                var key = line[..idx].Trim();
                if (!MetadataKeys.Contains(key))
                    return false;

                // D) get next non-space character
                var j = idx + 1;
                while (j < line.Length && char.IsWhiteSpace(line[j]))
                    j++;

                if (j >= line.Length)
                    return false;

                // E) must NOT be dialog opener
                return !IsDialogOpener(line[j]);
            }

            // Check if any unclosed brackets in text string
            static bool HasUnclosedBracket(string s)
            {
                if (string.IsNullOrEmpty(s))
                    return false;

                var hasOpen = false;
                var hasClose = false;

                foreach (var ch in s)
                {
                    if (!hasOpen && OpenBrackets.Contains(ch)) hasOpen = true;
                    if (!hasClose && CloseBrackets.Contains(ch)) hasClose = true;

                    if (hasOpen && hasClose)
                        break;
                }

                return hasOpen && !hasClose;
            }

            static bool IsAllAscii(string s)
            {
                for (var i = 0; i < s.Length; i++)
                    if (s[i] > 0x7F)
                        return false;
                return true;
            }

            static bool IsAllAsciiDigits(string s)
            {
                for (var i = 0; i < s.Length; i++)
                {
                    var ch = s[i];
                    if (ch > 0x7F || ch < '0' || ch > '9')
                        return false;
                }

                return s.Length > 0;
            }

            static bool IsAllCjk(string s)
            {
                for (var i = 0; i < s.Length; i++)
                {
                    var ch = s[i];

                    // treat common full-width space as not CJK heading content
                    if (char.IsWhiteSpace(ch))
                        return false;

                    if (!IsCjk(ch))
                        return false;
                }

                return s.Length > 0;
            }

            // Minimal CJK checker (BMP focused). You can swap with your existing one.
            static bool IsCjk(char ch)
            {
                var c = (int)ch;

                // CJK Unified Ideographs + Extension A
                if ((uint)(c - 0x3400) <= (0x4DBF - 0x3400)) return true;
                if ((uint)(c - 0x4E00) <= (0x9FFF - 0x4E00)) return true;

                // Compatibility Ideographs
                return (uint)(c - 0xF900) <= (0xFAFF - 0xF900);
            }

            static bool IsMixedCjkAscii(string s)
            {
                var hasCjk = false;
                var hasAscii = false;

                for (var i = 0; i < s.Length; i++)
                {
                    var ch = s[i];

                    if (ch <= 0x7F)
                    {
                        // ASCII letter/digit only (you can decide if punctuation counts)
                        if (char.IsLetterOrDigit(ch))
                            hasAscii = true;
                        else
                            return false; // reject ASCII punctuation in headings
                    }
                    else
                    {
                        if (IsCjk(ch))
                            hasCjk = true;
                        else
                            return false; // reject other scripts/symbols
                    }

                    if (hasCjk && hasAscii)
                        return true;
                }

                return false;
            }
        }

        // =========================================================
        //  Dialog state tracking
        // =========================================================

        /// <summary>
        /// Tracks the state of open or unmatched dialog quotation marks within
        /// the current paragraph buffer during PDF text reflow.
        ///
        /// This class is designed for incremental updates: callers feed each
        /// new line or text fragment into <see cref="Update(string?)"/>,
        /// allowing the state to evolve without rescanning previously processed
        /// text. This is essential for maintaining dialog continuity across
        /// broken PDF lines.
        /// </summary>
        private sealed class DialogState
        {
            /// <summary>
            /// Counter for unmatched CJK double quotes: “ ”.
            /// Increments on encountering “ and decrements on ”.
            /// </summary>
            private int _doubleQuote;

            /// <summary>
            /// Counter for unmatched CJK single quotes: ‘ ’.
            /// Increments on encountering ‘ and decrements on ’.
            /// </summary>
            private int _singleQuote;

            /// <summary>
            /// Counter for unmatched CJK corner quotes: 「 」.
            /// Increments on encountering 「 and decrements on 」.
            /// </summary>
            private int _corner;

            /// <summary>
            /// Counter for unmatched CJK bold corner quotes: 『 』.
            /// Increments on encountering 『 and decrements on 』.
            /// </summary>
            private int _cornerBold;

            /// <summary>
            /// Counter for unmatched upper corner brackets: ﹁ ﹂.
            /// </summary>
            private int _cornerTop;

            /// <summary>
            /// Counter for unmatched wide corner brackets: ﹃ ﹄.
            /// </summary>
            private int _cornerWide;

            /// <summary>
            /// Resets all quote counters to zero.
            /// Call this at the start of a new paragraph buffer.
            /// </summary>
            public void Reset()
            {
                _doubleQuote = 0;
                _singleQuote = 0;
                _corner = 0;
                _cornerBold = 0;
                _cornerTop = 0;
                _cornerWide = 0;
            }

            /// <summary>
            /// Updates the dialog state by scanning the provided text fragment.
            /// 
            /// Only characters representing CJK dialog punctuation are examined.
            /// Counters are increased for opening quotes and decreased for
            /// closing quotes (never below zero). This incremental approach
            /// avoids rescanning previously processed text and is safe even
            /// when PDF line breaks occur mid-dialog.
            /// </summary>
            /// <param name="s">
            /// A text fragment (typically one line or buffer chunk).
            /// If <c>null</c> or empty, the method performs no action.
            /// </param>
            public void Update(string? s)
            {
                if (string.IsNullOrEmpty(s))
                    return;

                foreach (var ch in s)
                {
                    switch (ch)
                    {
                        // ===== Double quotes =====
                        case '“': _doubleQuote++; break;
                        case '”':
                            if (_doubleQuote > 0) _doubleQuote--;
                            break;

                        // ===== Single quotes =====
                        case '‘': _singleQuote++; break;
                        case '’':
                            if (_singleQuote > 0) _singleQuote--;
                            break;

                        // ===== Corner brackets =====
                        case '「': _corner++; break;
                        case '」':
                            if (_corner > 0) _corner--;
                            break;

                        // ===== Bold corner brackets =====
                        case '『': _cornerBold++; break;
                        case '』':
                            if (_cornerBold > 0) _cornerBold--;
                            break;

                        // ===== NEW: vertical brackets (﹁ ﹂) =====
                        case '﹁': _cornerTop++; break;
                        case '﹂':
                            if (_cornerTop > 0) _cornerTop--;
                            break;

                        // ===== NEW: vertical bold brackets (﹃ ﹄) =====
                        case '﹃': _cornerWide++; break;
                        case '﹄':
                            if (_cornerWide > 0) _cornerWide--;
                            break;
                    }
                }
            }

            /// <summary>
            /// Gets a value indicating whether any dialog quote type is
            /// currently left unclosed. When <c>true</c>, the current paragraph
            /// buffer is considered to be inside an ongoing dialog segment, and
            /// reflow logic should avoid forcing paragraph breaks until closure.
            /// </summary>
            public bool IsUnclosed =>
                _doubleQuote > 0 || _singleQuote > 0 || _corner > 0 || _cornerBold > 0 || _cornerTop > 0 ||
                _cornerWide > 0;
        }

        // =========================================================
        //  Helper methods (IsHeadingLike, IsCjk, etc.)
        // =========================================================

        private static string StripHalfWidthIndentKeepFullWidth(string s)
        {
            var i = 0;

            // Strip only halfwidth spaces at left
            while (i < s.Length && s[i] == ' ')
                i++;

            return s.Substring(i);
        }

        // ------------------------------------------------------------
        // Style-layer repeat collapse for PDF headings / title lines.
        //
        // Conceptually this emulates a regex like:
        //
        //    (.{4,10}?)\1{2,3}
        //
        // i.e. “a phrase of length 4–10 chars, repeated 3–4 times”,
        // but implemented in a token- and phrase-aware way so we can
        // correctly handle CJK titles and multi-word headings.
        //
        // This routine is intentionally conservative:
        //   - It targets layout / styling noise (highlighted titles,
        //     duplicated TOC entries, etc.).
        //   - It avoids collapsing natural language like “哈哈哈哈哈哈”.
        // ------------------------------------------------------------
        private static string CollapseRepeatedSegments(string line)
        {
            if (string.IsNullOrEmpty(line))
                return line;

            // Split on whitespace into discrete tokens.
            // Typical headings have 1–3 tokens; TOC / cover captions may have more.
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return line;

            // 1) Phrase-level collapse:
            //    Detect and collapse repeated *word sequences*, e.g.:
            //
            //    "背负着一切的麒麟 背负着一切的麒麟 背负着一切的麒麟 背负着一切的麒麟"
            //      → "背负着一切的麒麟"
            //
            //    "（第一季大结局） （第一季大结局） （第一季大结局） （第一季大结局）"
            //      → "（第一季大结局）"
            //
            parts = CollapseRepeatedWordSequences(parts);

            // 2) Token-level collapse:
            //    As a fallback, if an individual token itself is made of
            //    a repeated substring (e.g. "abcdabcdabcd"), collapse it:
            //
            //      "abcdabcdabcd" → "abcd"
            //
            //    This is carefully tuned so we do *not* destroy natural
            //    short repeats such as "哈哈哈哈哈哈".
            for (var i = 0; i < parts.Length; i++)
            {
                parts[i] = CollapseRepeatedToken(parts[i]);
            }

            // Re-join with a single space between tokens.
            return string.Join(" ", parts);
        }

        /// <summary>
        /// Collapses repeated sequences of tokens (phrases) within a line.
        ///
        /// This targets PDF-styled headings where the same phrase is rendered
        /// 3–4 times for emphasis, for example:
        ///
        ///   「背负着一切的麒麟 背负着一切的麒麟 背负着一切的麒麟 背负着一切的麒麟」
        ///
        /// The algorithm:
        ///   - Scans for candidate phrases of length 1 to <c>maxPhraseLen</c> tokens.
        ///   - If the same phrase occurs consecutively at least <c>minRepeats</c>
        ///     times (default = 3), all repeats are collapsed into a single copy.
        ///   - Prefix and suffix tokens are preserved.
        ///
        /// This is intentionally conservative to avoid collapsing normal text,
        /// while effectively removing layout/styling repetition in headings.
        /// </summary>
        private static string[] CollapseRepeatedWordSequences(string[] parts)
        {
            const int minRepeats = 3; // minimum number of consecutive repeats required
            const int maxPhraseLen = 8; // typical heading phrases are short

            var n = parts.Length;
            if (n < minRepeats)
                return parts;

            // Scan from left to right for any repeating phrase.
            for (var start = 0; start < n; start++)
            {
                for (var phraseLen = 1; phraseLen <= maxPhraseLen && start + phraseLen <= n; phraseLen++)
                {
                    // phrase = parts[start .. start+phraseLen-1]
                    var count = 1;

                    while (true)
                    {
                        var nextStart = start + count * phraseLen;
                        if (nextStart + phraseLen > n)
                            break;

                        var equal = true;
                        for (var k = 0; k < phraseLen; k++)
                        {
                            if (parts[start + k].Equals(parts[nextStart + k], StringComparison.Ordinal)) continue;
                            equal = false;
                            break;
                        }

                        if (!equal)
                            break;

                        count++;
                    }

                    if (count < minRepeats) continue;
                    {
                        // Build collapsed list:
                        //   [prefix] + [one phrase] + [tail]
                        var result = new List<string>(n - (count - 1) * phraseLen);

                        // Prefix before the repeated phrase.
                        for (var i = 0; i < start; i++)
                            result.Add(parts[i]);

                        // Single copy of the repeated phrase.
                        for (var k = 0; k < phraseLen; k++)
                            result.Add(parts[start + k]);

                        // Tail after all repeats.
                        var tailStart = start + count * phraseLen;
                        for (var i = tailStart; i < n; i++)
                            result.Add(parts[i]);

                        return result.ToArray();
                    }
                }
            }

            return parts;
        }

        /// <summary>
        /// Collapses a single token if it is composed entirely of a repeated
        /// substring, where the base unit is between 4 and 10 characters and
        /// appears at least 3 times.
        ///
        /// Examples:
        ///   "abcdabcdabcd"      → "abcd"
        ///   "第一季大结局第一季大结局第一季大结局" → "第一季大结局"
        ///
        /// Very short units (length &lt; 4) are ignored on purpose to avoid
        /// collapsing natural language patterns such as "哈哈哈哈哈哈".
        /// </summary>
        private static string CollapseRepeatedToken(string token)
        {
            // Very short tokens or huge ones are unlikely to be styled repeats.
            if (token.Length is < 4 or > 200)
                return token;

            // Try unit sizes between 4 and 10 chars, and require at least
            // 3 repeats (N >= 3). This corresponds roughly to a pattern like:
            //
            //   (.{4,10}?)\1{2,}
            //
            // but constrained to exactly fill the entire token.
            for (var unitLen = 4; unitLen <= 10 && unitLen <= token.Length / 3; unitLen++)
            {
                if (token.Length % unitLen != 0)
                    continue;

                var unit = token[..unitLen];
                var allMatch = true;

                for (var pos = 0; pos < token.Length; pos += unitLen)
                {
                    if (token.AsSpan(pos, unitLen).SequenceEqual(unit)) continue;
                    allMatch = false;
                    break;
                }

                if (allMatch)
                {
                    // Token is just [unit] repeated N times (N >= 3):
                    // collapse it to a single unit.
                    return unit;
                }
            }

            return token;
        }
    }
}