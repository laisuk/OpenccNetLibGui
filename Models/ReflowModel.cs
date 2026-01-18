using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

        // Chapter / heading patterns (短行 + 第N章/卷/节/部, 前言/序章/终章/尾声/番外)
        private static readonly Regex TitleHeadingRegex =
            new(
                @"^(?!.*[,，])(?=.{0,50}$)
                  (前言|序章|楔子|终章|尾声|后记|尾聲|後記|番外.{0,15}
                  |.{0,10}?第.{0,5}?([章节部卷節回][^分合的])|(?:卷|章)[一二三四五六七八九十](?:$|.{0,20}?)
                  )",
                RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

        //Paragraph indentation
        private static readonly Regex IndentRegex =
            new(@"^[\s\u3000]{2,}", RegexOptions.Compiled);

        // -----------------------------------------------------------------------------
        // Metadata heading title names
        // -----------------------------------------------------------------------------

        // Metadata heading title names
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
            "内容標簽", "内容标签",
            "系列",

            // ===== 7. Publishing Cycle =====
            "發行日", "发行日",
            "初版",

            // ===== 8. Common keys without variants =====
            "ISBN"
        };

        // NOTE:
        // MaxMetadataKeyLength is derived from MetadataKeys (single policy owner).
        // Do NOT hardcode or duplicate this limit elsewhere.
        private static readonly int MaxMetadataKeyLength = MetadataKeys.Max(k => k.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsMetadataKey(ReadOnlySpan<char> keySpan)
        {
            keySpan = TrimWhitespace(keySpan);
            if (keySpan.Length == 0 || keySpan.Length > MaxMetadataKeyLength)
                return false;

            return MetadataKeys.Contains(keySpan.ToString());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<char> TrimWhitespace(ReadOnlySpan<char> s)
        {
            var start = 0;
            while (start < s.Length && char.IsWhiteSpace(s[start])) start++;

            var end = s.Length - 1;
            while (end >= start && char.IsWhiteSpace(s[end])) end--;

            return s.Slice(start, end - start + 1);
        }


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
        /// <param name="text">
        ///     Raw text extracted from a PDF (via PdfPig, Pdfium, or any other engine).
        ///     The input is expected to be line-based with newline separators.
        /// </param>
        /// <param name="addPdfPageHeader">
        ///     If <c>true</c>, PDF page headers of the form <c>"=== [Page X/Y] ==="</c>
        ///     are preserved during reflow.  
        ///     If <c>false</c>, page markers (including markers inserted during extraction)
        ///     are removed during reconstruction.
        /// </param>
        /// <param name="compact">
        ///     Determines output formatting style:
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 <c>true</c> — Compact mode:  
        ///                 Produces one line per paragraph with no blank lines in between.
        ///                 Ideal for dictionary building, NLP preprocessing, and plain text exports.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 <c>false</c> — Novel mode:  
        ///                 Inserts a blank line between paragraphs, matching book-style formatting.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </param>
        /// <param name="shortHeading">
        ///     Configuration object that controls how a line is classified as a
        ///     <em>short heading</em> during CJK paragraph reflow.
        /// 
        ///     <para>
        ///         The classification is based on a combination of:
        ///     </para>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 <b>Maximum length</b> (<see cref="ShortHeadingSettings.MaxLen"/>):
        ///                 Lines longer than this value are never considered headings.
        ///                 Typical range is 5–15 characters; default is 8.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 <b>Allowed character patterns</b>, such as:
        ///                 all CJK characters, all ASCII characters, ASCII digits only,
        ///                 or mixed CJK + ASCII (controlled by the corresponding flags
        ///                 in <see cref="ShortHeadingSettings"/>).
        ///             </description>
        ///         </item>
        ///     </list>
        /// 
        ///     <para>
        ///         Before pattern matching, several <b>absolute rejection rules</b> are applied:
        ///         lines containing sentence-ending punctuation, commas or list separators,
        ///         unclosed brackets, or PDF page markers are never treated as headings,
        ///         even if they satisfy length and pattern constraints.
        ///     </para>
        /// 
        ///     <para>
        ///         This rule-based approach avoids hard-coded language assumptions and allows
        ///         users to fine-tune heading detection behavior for different document styles,
        ///         including novels, technical documents, and bilingual (CJK + English) texts.
        ///     </para>
        /// </param>
        /// <param name="sentenceBoundaryLevel"></param>
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
            ShortHeadingSettings? shortHeading = null,
            int sentenceBoundaryLevel = 2)
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

            var customTitleRx = shortHeading.CustomTitleHeadingRegexCompiled;
            var hasCustomTitleRegex = customTitleRx != null;

            foreach (var rawLine in lines)
            {
                // 1) Visual form: keep full-width indent, drop half-width indent on the left, trim only right side
                var stripped = rawLine.TrimEnd();
                stripped = StripHalfWidthIndentKeepFullWidth(stripped);

                // 2) Probe form (for structural / heading detection): remove all indentation
                var probe = stripped.TrimStart(' ', '\u3000');

                // 🧱 ABSOLUTE STRUCTURAL RULE — must be first (run on probe, output stripped)
                if (PunctSets.IsVisualDividerLine(probe))
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

                // 🔹 NEW: collapse style-layer repeated segments *before* heading detection
                stripped = CollapseRepeatedSegments(stripped);

                // 3) Logical form for heading detection: no indent at all
                var headingProbe = stripped.TrimStart(' ', '\u3000');

                // NEW: user-defined title heading regex (runs right after built-in title check)
                var isCustomTitleHeading = hasCustomTitleRegex && customTitleRx!.IsMatch(headingProbe);
                var isTitleHeading = TitleHeadingRegex.IsMatch(headingProbe);
                var isShortHeading = IsHeadingLike(stripped, shortHeading);
                var isMetadata = IsMetadataLine(stripped); // 〈── New

                var hasBuffer = buffer.Length > 0;
                bool? hasUnclosedBracketLazy = null;

                bool HasUnclosedBracket()
                {
                    if (!hasBuffer)
                        return false;

                    return hasUnclosedBracketLazy ??=
                        PunctSets.HasUnclosedBracket(buffer);
                }

                // 1) Empty line
                if (stripped.Length == 0)
                {
                    if (!addPdfPageHeader && buffer.Length > 0)
                    {
                        // NEW: If dialog is unclosed, always treat blank line as soft (cross-page artifact).
                        // Never flush mid-dialog just because we saw a blank line.
                        if (dialogState.IsUnclosed || HasUnclosedBracket())
                            continue;

                        // Light rule: only flush on blank line if buffer ends with STRONG sentence end.
                        // Otherwise, treat as a soft cross-page blank line and keep accumulating.
                        if (PunctSets.TryGetLastNonWhitespace(buffer, out _, out var last) &&
                            !PunctSets.IsStrongSentenceEnd(last))
                        {
                            continue;
                        }
                    }

                    // End of paragraph → flush buffer (do NOT emit "")
                    if (buffer.Length > 0)
                    {
                        segments.Add(buffer.ToString());
                        buffer.Clear();
                        dialogState.Reset();
                    }

                    // IMPORTANT: Emitting empty segments would introduce
                    // hard paragraph boundaries and break cross-line reflow
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

                // 3) New: Custom title heading regex (Advanced)
                // Custom overrides built-in: user intent > heuristics.
                if (isCustomTitleHeading)
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

                // 4) Titles (default)
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

                // 5) Metadata 行（短 key:val，如「書名：xxx」「作者：yyy」）
                if (isMetadata)
                {
                    if (buffer.Length > 0)
                    {
                        segments.Add(buffer.ToString());
                        buffer.Clear();
                        dialogState.Reset();
                    }

                    // Metadata 每行獨立存放（之後可以決定係 skip、折疊、顯示）
                    segments.Add(stripped);
                    continue;
                }

                // 6) Weak heading-like:
                //     Only takes effect when the “previous paragraph is safe”
                //     AND “the previous paragraph’s ending looks like a sentence boundary”.
                if (isShortHeading)
                {
                    var isAllCjk = CjkText.IsAllCjkIgnoringWhitespace(stripped);

                    bool splitAsHeading;
                    if (buffer.Length == 0)
                    {
                        // Start of document / just flushed
                        splitAsHeading = true;
                    }
                    else
                    {
                        if (HasUnclosedBracket())
                        {
                            // Unsafe previous paragraph → must be continuation
                            splitAsHeading = false;
                        }
                        else
                        {
                            if (!PunctSets.TryGetLastTwoNonWhitespace(buffer, out _, out var last, out _, out _))
                            {
                                // Buffer is whitespace-only → treat like empty
                                splitAsHeading = true;
                            }
                            else
                            {
                                var prevEndsWithCommaLike = PunctSets.IsCommaLike(last);
                                var prevEndsWithSentencePunct = PunctSets.IsClauseOrEndPunct(last);

                                var currentLooksLikeContinuationMarker =
                                    isAllCjk
                                    || PunctSets.EndsWithColonLike(stripped)
                                    || PunctSets.EndsWithAllowedPostfixCloser(stripped);

                                // Comma-ending → continuation
                                if (prevEndsWithCommaLike)
                                    splitAsHeading = false;
                                // All-CJK short heading-like + previous not ended → continuation
                                else if (currentLooksLikeContinuationMarker && !prevEndsWithSentencePunct)
                                    splitAsHeading = false;
                                else
                                    splitAsHeading = true;
                            }
                        }
                    }

                    if (splitAsHeading)
                    {
                        // If we have a real previous paragraph, flush it first
                        if (buffer.Length > 0)
                        {
                            segments.Add(buffer.ToString());
                            buffer.Clear();
                            dialogState.Reset();
                        }

                        // Current line becomes a standalone heading
                        segments.Add(stripped);
                        continue;
                    }

                    // else: fall through → normal merge logic below
                }

                // 7) Finalizer: strong sentence end → flush immediately. Do not remove.
                // If the current line completes a strong sentence, append it and flush immediately.
                if (buffer.Length > 0
                    && !dialogState.IsUnclosed
                    && !HasUnclosedBracket()
                    && PunctSets.EndsWithStrongSentenceEnd(stripped))
                {
                    buffer.Append(stripped); // buffer now has new value
                    segments.Add(buffer.ToString()); // This is not old bufferText (it had been updated)
                    buffer.Clear();
                    dialogState.Reset();
                    // dialogState.Update(stripped);
                    continue;
                }

                // 8) First line inside buffer → start of a new paragraph
                // No boundary note here — flushing is handled later (Rule 10).
                if (buffer.Length == 0)
                {
                    buffer.Append(stripped);
                    dialogState.Reset();
                    dialogState.Update(stripped);
                    continue;
                }

                // *** DIALOG: treat any line that *starts* with a dialog opener as a new paragraph
                var currentIsDialogStart = PunctSets.BeginWithDialogStarter(stripped);
                
                // 🔸 9a) NEW RULE: If previous line ends with comma, 
                //     do NOT flush even if this line starts dialog.
                //     (comma-ending means the sentence is not finished)
                if (currentIsDialogStart)
                {
                    var shouldFlushPrev = false;

                    if (buffer.Length > 0 &&
                        PunctSets.TryGetLastNonWhitespace(buffer, out _, out var last))
                    {
                        var isContinuation =
                            PunctSets.IsCommaLike(last) ||
                            CjkText.IsCjk(last) ||
                            dialogState.IsUnclosed ||
                            HasUnclosedBracket();

                        shouldFlushPrev = !isContinuation;
                    }

                    if (shouldFlushPrev)
                    {
                        segments.Add(buffer.ToString());
                        buffer.Clear();
                    }

                    // Start (or continue) the dialog paragraph
                    buffer.Append(stripped);
                    dialogState.Reset();
                    dialogState.Update(stripped);
                    continue;
                }

                // 🔸 9b) Dialog end line: ends with dialog closer.
                // Flush when the char before closer is strong end,
                // and bracket safety is satisfied (with a narrow typo override).
                if (PunctSets.TryGetLastNonWhitespace(stripped, out var lastIdx, out var lastCh) &&
                    PunctSets.IsDialogCloser(lastCh))
                {
                    // Check punctuation right before the closer (e.g., “？” / “。”)
                    var punctBeforeCloserIsStrong =
                        PunctSets.TryGetPrevNonWhitespace(stripped, lastIdx, out var prevCh) &&
                        PunctSets.IsClauseOrEndPunct(prevCh);

                    // Snapshot bracket safety BEFORE appending current line
                    var bufferHasBracketIssue = HasUnclosedBracket();
                    var lineHasBracketIssue = PunctSets.HasUnclosedBracket(stripped);

                    buffer.Append(stripped);
                    dialogState.Update(stripped);

                    // Allow flush if:
                    // - dialog is closed after this line
                    // - punctuation before closer is a strong end
                    // - and either:
                    //     (a) buffer has no bracket issue, OR
                    //     (b) buffer has bracket issue but this line itself is the culprit (OCR/typo),
                    //        so allow a dialog-end flush anyway.
                    if (!dialogState.IsUnclosed &&
                        punctBeforeCloserIsStrong &&
                        (!bufferHasBracketIssue || lineHasBracketIssue))
                    {
                        segments.Add(buffer.ToString());
                        buffer.Clear();
                        dialogState.Reset();
                    }

                    continue;
                }

                // 9b) NEW RULE: colon + dialog continuation
                // e.g. "她寫了一行字：" + "「如果連自己都不相信……」"
                // if (PunctSets.EndsWithColonLike(buffer))
                // {
                //     if (stripped.Length > 0 && PunctSets.IsDialogOpener(stripped[0]))
                //     {
                //         buffer.Append(stripped);
                //         dialogState.Update(stripped);
                //         continue;
                //     }
                // }

                // NOTE: we *do* block splits when dialogState.IsUnclosed,
                // so multi-line dialog stays together. Once all quotes are
                // closed, CJK punctuation may end the paragraph as usual.

                switch (dialogState.IsUnclosed)
                {
                    // 10a) Strong sentence boundary → new paragraph
                    // Triggered by full-width CJK sentence-ending punctuation (。！？ etc.)
                    // NOTE: Dialog safety gate has the highest priority.
                    // If dialog quotes/brackets are not closed, never split the paragraph.
                    case false when CjkText.EndsWithSentenceBoundary(buffer, level: sentenceBoundaryLevel)
                                    && !HasUnclosedBracket():

                    // 10b) Closing CJK bracket boundary → new paragraph
                    // Handles cases where a paragraph ends with a full-width closing bracket/quote
                    // (e.g. ）】》」) and should not be merged with the next line.
                    case false when CjkText.EndsWithCjkBracketBoundary(buffer):

                    // 10c) Indentation → new paragraph
                    // Pre-append rule:
                    // Indentation indicates a new paragraph starts on this line.
                    // Flush the previous buffer and immediately seed the next paragraph.
                    case false when buffer.Length > 0 && IndentRegex.IsMatch(rawLine):
                        segments.Add(buffer.ToString());
                        buffer.Clear();
                        buffer.Append(stripped);
                        dialogState.Reset();
                        dialogState.Update(stripped);
                        continue;
                }

                // Removed legacy chapter-ending safety check; behavior covered by sentence-boundary logi
                // 11) Chapter-like endings: 章 / 节 / 部 / 卷 (with trailing brackets)
                // if (!dialogState.IsUnclosed &&
                //     bufferText.Length <= 12 &&
                //     IsMostlyCjk(bufferText) &&
                //     Regex.IsMatch(bufferText, @"(章|节|部|卷|節|回)[】》〗〕〉」』）]*$") &&
                //     !ContainsAny(bufferText, '，', ',', '、', '。', '！', '？', '：', ':', ';'))
                // {
                //     segments.Add(bufferText);
                //     buffer.Clear();
                //     buffer.Append(stripped);
                //     dialogState.Reset();
                //     dialogState.Update(stripped);
                //     continue;
                // }

                // 12) Default merge (soft line break)
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

            static bool IsHeadingLike(ReadOnlySpan<char> s, ShortHeadingSettings sh)
            {
                s = TrimSpan(s);
                if (s.IsEmpty)
                    return false;

                // keep page markers intact
                if (StartsWith(s, "=== ".AsSpan()) && EndsWith(s, "===".AsSpan()))
                    return false;

                // Reject headings with unclosed brackets
                if (PunctSets.HasUnclosedBracket(s)) // <-- add span overload (you already have it!)
                    return false;

                // Get last meaningful character (robust against whitespace changes)
                if (!PunctSets.TryGetLastNonWhitespace(s, out var lastIdx, out var last))
                    return false;

                // Clamp maxLen
                var baseMax = Math.Clamp(sh.MaxLen, 3, 30);
                var len = s.Length;

                // Short circuit for item title-like: "物品准备："
                if (PunctSets.IsColonLike(last) &&
                    len <= sh.MaxLen &&
                    // CjkText.IsAllCjk(s[..lastIdx], allowWhitespace: false))
                    CjkText.IsAllCjkNoWhiteSpace(s[..lastIdx]))
                {
                    return true;
                }

                // Bracket-wrapped standalone structural line (e.g. 《书名》 / 【组成】 / （附录）)
                if (PunctSets.IsWrappedByMatchingBracket(s, last) && CjkText.IsMostlyCjk(s))
                {
                    return true;
                }

                if (PunctSets.IsAllowedPostfixCloser(last) && !PunctSets.ContainsAnyCommaLike(s))
                {
                    return true;
                }

                // Reject any other clause or End Punct
                if (PunctSets.IsClauseOrEndPunct(last))
                    return false;

                // Reject any short line containing comma-like separators
                if (PunctSets.ContainsAnyCommaLike(s))
                    return false;

                // ASCII headings can be longer
                var effectiveMax = baseMax;

                var isAllAscii = sh.AllAsciiEnabled && CjkText.IsAllAscii(s);
                var isMixed = sh.MixedCjkAsciiEnabled && CjkText.IsMixedCjkAscii(s);

                if (isAllAscii || isMixed)
                {
                    effectiveMax = Math.Clamp(baseMax * 2, 10, 30);
                }

                if (len > effectiveMax)
                    return false;

                // Reject if the candidate contains any strong sentence-ending punctuation.
                // Soft punctuation (comma-like, colon, etc.) is allowed in heading candidates.
                if (PunctSets.ContainsStrongSentenceEnd(s))
                    return false;

                // ---- Pattern checks ----
                return (sh.AllAsciiEnabled && CjkText.IsAllAscii(s))
                       || (sh.AllCjkEnabled && CjkText.IsAllCjk(s, allowWhitespace: false))
                       || (sh.AllAsciiDigitsEnabled && CjkText.IsAllAsciiDigits(s))
                       || (sh.MixedCjkAsciiEnabled && CjkText.IsMixedCjkAscii(s));
            }

            static ReadOnlySpan<char> TrimSpan(ReadOnlySpan<char> s)
            {
                var start = 0;
                var end = s.Length - 1;

                while (start <= end && char.IsWhiteSpace(s[start])) start++;
                while (end >= start && char.IsWhiteSpace(s[end])) end--;

                return start > end ? ReadOnlySpan<char>.Empty : s.Slice(start, end - start + 1);
            }

            static bool StartsWith(ReadOnlySpan<char> s, ReadOnlySpan<char> prefix)
            {
                return s.Length >= prefix.Length && s[..prefix.Length].SequenceEqual(prefix);
            }

            static bool EndsWith(ReadOnlySpan<char> s, ReadOnlySpan<char> suffix)
            {
                return s.Length >= suffix.Length && s[^suffix.Length..].SequenceEqual(suffix);
            }

            static bool IsMetadataLine(ReadOnlySpan<char> line)
            {
                if (line.IsEmpty)
                    return false;

                // Equivalent to string.IsNullOrWhiteSpace
                var firstNonWs = 0;
                while (firstNonWs < line.Length && char.IsWhiteSpace(line[firstNonWs]))
                    firstNonWs++;

                if (firstNonWs >= line.Length)
                    return false;

                // original: if (line.Length > 30) return false;
                // (keep semantics: uses raw line length, not trimmed length)
                if (line.Length > 30)
                    return false;

                var idx = -1; // separator index
                var j = -1; // first non-ws after separator

                for (var i = firstNonWs; i < line.Length; i++)
                {
                    if (!PunctSets.IsMetadataSeparator(line[i]))
                        continue;

                    idx = i;

                    j = i + 1;
                    while (j < line.Length && char.IsWhiteSpace(line[j]))
                        j++;

                    break;
                }

                // must have a separator and content after it
                if (idx < 0 || j < 0 || j >= line.Length)
                    return false;

                // structural early reject (ignore leading whitespace)
                var rawKeyLen = idx - firstNonWs;
                if (rawKeyLen <= 0 || rawKeyLen > MaxMetadataKeyLength)
                    return false;

                // semantic owner
                if (!IsMetadataKey(line.Slice(firstNonWs, rawKeyLen)))
                    return false;

                // Must not start value with dialog opener
                return !PunctSets.IsDialogOpener(line[j]);
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

            return s[i..];
        }

        // private static ReadOnlySpan<char> NormalizeLeadingDoubleDialogOpener(ReadOnlySpan<char> s)
        // {
        //     if (s.Length >= 2 && PunctSets.IsDialogOpener(s[0]) && s[0] == s[1])
        //         return s[1..];
        //     return s;
        // }

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
            //    a repeated substring (e.g. "AbcdAbcdAbcd"), collapse it:
            //
            //      "AbcdAbcdAbcd" → "Abcd"
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
        ///   "AbcdAbcdAbcd"      → "Abcd"
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