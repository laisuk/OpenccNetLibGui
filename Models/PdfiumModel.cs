// ReSharper disable InconsistentNaming

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace OpenccNetLibGui.Models;

internal static class PdfiumModel
{
    // -------------------------------
    //  Public APIs
    // -------------------------------

    /// <summary>
    /// Synchronously extracts plain text from a PDF file using PDFium.
    /// </summary>
    /// <param name="pdfPath">
    /// Full path to the PDF file to open.
    /// </param>
    /// <param name="ignoreUntrustedPdfText">
    /// If <c>true</c>, performs object-level text extraction and skips
    /// repeated overlay- or watermark-like text objects. This is an
    /// extraction-only filter and does not modify the original PDF.
    /// </param>
    /// <returns>
    /// The concatenated plain-text content of all pages in the PDF, with
    /// page markers inserted in the form <c>=== [Page X/Y] ===</c>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="pdfPath"/> is <c>null</c>, empty,
    /// or consists only of whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when PDFium fails to load the document
    /// (i.e. <c>FPDF_LoadDocument</c> returns <c>NULL</c>).
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method is a synchronous entry point into the PDFium-based
    /// extraction pipeline and always emits page header markers
    /// (<c>addPdfPageHeader</c> is implicitly enabled).
    /// </para>
    /// <para>
    /// When <paramref name="ignoreUntrustedPdfText"/> is enabled, the extractor
    /// prefers content text and may intentionally skip repeated overlay text
    /// commonly used for watermarking or anti-copy purposes. This behavior
    /// affects extraction results only and does not alter the source document.
    /// </para>
    /// <para>
    /// Only PDFs containing embedded, selectable text are supported. Scanned or
    /// image-only PDFs will not produce meaningful output unless they have been
    /// OCR-processed beforehand.
    /// </para>
    /// </remarks>
    public static PdfLoadResult ExtractText(string pdfPath, bool ignoreUntrustedPdfText)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            throw new ArgumentException("PDF path is required.", nameof(pdfPath));

        PdfiumNative.FPDF_InitLibrary();
        try
        {
            var doc = PdfiumNative.FPDF_LoadDocument(pdfPath, null);
            if (doc == IntPtr.Zero)
                throw new InvalidOperationException("FPDF_LoadDocument failed (doc == NULL).");

            try
            {
                return ExtractPages(
                    doc,
                    addPdfPageHeader: true,
                    ignoreUntrustedPdfText: ignoreUntrustedPdfText,
                    statusCallback: null,
                    cancellationToken: CancellationToken.None);
            }
            finally
            {
                PdfiumNative.FPDF_CloseDocument(doc);
            }
        }
        finally
        {
            PdfiumNative.FPDF_DestroyLibrary();
        }
    }

    // --------------------------------------------------------------
    //  ExtractTextAsync (with optional progress callback, consistent with PdfPig)
    // --------------------------------------------------------------

    /// <summary>
    /// Asynchronously extracts plain text from a PDF file using PDFium,
    /// with optional page headers, overlay-text filtering, and progress reporting.
    /// </summary>
    /// <param name="pdfPath">
    /// Full path to the PDF file to open.
    /// </param>
    /// <param name="addPdfPageHeader">
    /// If <c>true</c>, each page is prefixed with a header marker in the form
    /// <c>=== [Page X/Y] ===</c>, matching the PdfPig-based extractor and aiding
    /// downstream reflow or debugging.
    /// </param>
    /// <param name="ignoreUntrustedPdfText">
    /// If <c>true</c>, performs object-level text extraction and skips repeated
    /// overlay- or watermark-like text objects. This is an extraction-only filter
    /// and does not modify the original PDF.
    /// </param>
    /// <param name="progressCallback">
    /// Optional callback invoked periodically with overall progress as a percentage
    /// (0–100).
    /// </param>
    /// <param name="cancellationToken">
    /// Token that can be used to cancel the operation.
    /// If cancellation is requested, an <see cref="OperationCanceledException"/> is thrown.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation, whose result is the
    /// concatenated plain-text content of all pages in the PDF.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="pdfPath"/> is <c>null</c>, empty,
    /// or consists only of whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when PDFium fails to load the document
    /// (i.e. <c>FPDF_LoadDocument</c> returns <c>NULL</c>).
    /// </exception>
    /// <remarks>
    /// <para>
    /// The extraction work is executed on a background thread using
    /// <see cref="Task.Run{TResult}(Func{TResult}, CancellationToken)"/>,
    /// making this method suitable for UI applications that must remain responsive
    /// while processing large PDFs.
    /// </para>
    /// <para>
    /// Only PDFs containing embedded, selectable text are supported. Scanned or
    /// image-only PDFs will not produce meaningful output unless they have been
    /// OCR-processed beforehand.
    /// </para>
    /// </remarks>
    internal static Task<PdfLoadResult> ExtractTextAsync(
        string pdfPath,
        bool addPdfPageHeader,
        bool ignoreUntrustedPdfText,
        Action<int>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            throw new ArgumentException("PDF path is required.", nameof(pdfPath));

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            PdfiumNative.FPDF_InitLibrary();
            try
            {
                var doc = PdfiumNative.FPDF_LoadDocument(pdfPath, null);
                if (doc == IntPtr.Zero)
                    throw new InvalidOperationException("FPDF_LoadDocument failed (doc == NULL).");

                try
                {
                    void Progress(int pageIndex, int percent)
                    {
                        progressCallback?.Invoke(percent);
                    }

                    return ExtractPages(
                        doc,
                        addPdfPageHeader,
                        ignoreUntrustedPdfText,
                        Progress,
                        cancellationToken);
                }
                finally
                {
                    PdfiumNative.FPDF_CloseDocument(doc);
                }
            }
            finally
            {
                PdfiumNative.FPDF_DestroyLibrary();
            }
        }, cancellationToken);
    }


    // -----------------------------------------------------------------------------
    //  Shared core: PDFium page loop
    // -----------------------------------------------------------------------------

    /// <summary>
    /// Core PDFium-based page iteration routine shared by both synchronous and
    /// asynchronous extraction entry points.
    /// </summary>
    /// <param name="doc">
    /// An open PDFium document handle obtained from <c>FPDF_LoadDocument</c>.
    /// Ownership of the handle remains with the caller; this method does not
    /// close the document.
    /// </param>
    /// <param name="addPdfPageHeader">
    /// If <c>true</c>, emits a page header marker in the form
    /// <c>=== [Page X/Y] ===</c> before the content of each page.
    /// </param>
    /// <param name="ignoreUntrustedPdfText">
    /// If <c>true</c>, uses object-level extraction and skips repeated
    /// overlay- or watermark-like text objects. This filter affects extraction
    /// results only and does not modify the source PDF.
    /// </param>
    /// <param name="statusCallback">
    /// Optional progress callback invoked with the zero-based page index and an
    /// overall completion percentage (0–100).
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation. If cancellation is requested,
    /// an <see cref="OperationCanceledException"/> is thrown.
    /// </param>
    /// <returns>
    /// A <see cref="PdfLoadResult"/> containing the concatenated plain-text
    /// content of all processed pages and the total page count.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method performs a single pass over all pages in the document,
    /// loading each page, extracting its text, and appending the result to
    /// a shared buffer. Page headers and blank page separators are emitted
    /// consistently to preserve page boundaries.
    /// </para>
    /// <para>
    /// A reusable UTF-16 buffer is allocated once and passed by reference to
    /// the flattened text extractor to minimize per-page allocations.
    /// </para>
    /// <para>
    /// If the document contains no pages (<c>FPDF_GetPageCount</c> &lt;= 0),
    /// the method reports completion (100%) to <paramref name="statusCallback"/>
    /// (if provided) and returns an empty result.
    /// </para>
    /// </remarks>
    private static PdfLoadResult ExtractPages(
        IntPtr doc,
        bool addPdfPageHeader,
        bool ignoreUntrustedPdfText,
        Action<int, int>? statusCallback, // (pageIndex, percent)
        CancellationToken cancellationToken)
    {
        var pageCount = PdfiumNative.FPDF_GetPageCount(doc);
        if (pageCount <= 0)
        {
            statusCallback?.Invoke(0, 100);
            return new PdfLoadResult(string.Empty, 0);
        }

        static int GetProgressBlock(int totalPages) => totalPages switch
        {
            <= 20 => 1,
            <= 100 => 3,
            <= 300 => 5,
            _ => Math.Max(1, totalPages / 20)
        };

        var block = GetProgressBlock(pageCount);
        var sb = new StringBuilder();

        // Reusable UTF-16 buffer for flattened extraction
        ushort[]? buffer = null;

        for (var i = 0; i < pageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Progress reporting at adaptive intervals
            if (statusCallback != null &&
                (i == 0 || i == pageCount - 1 || i % block == 0))
            {
                var percent = (int)(((double)(i + 1) / pageCount) * 100);
                statusCallback(i, percent);
            }

            IntPtr page = IntPtr.Zero;
            IntPtr textPage = IntPtr.Zero;

            try
            {
                page = PdfiumNative.FPDF_LoadPage(doc, i);
                if (page == IntPtr.Zero)
                    continue;

                textPage = PdfiumNative.FPDFText_LoadPage(page);
                if (textPage == IntPtr.Zero)
                    continue;

                var text = ignoreUntrustedPdfText
                    ? ExtractPageText_IgnoreUntrusted(page, textPage)
                    : ExtractPageText(textPage, ref buffer);

                // Explicit handling of empty / whitespace-only pages
                if (string.IsNullOrWhiteSpace(text))
                {
                    if (addPdfPageHeader)
                        sb.AppendLine($"=== [Page {i + 1}/{pageCount}] ===");

                    // Always emit a visible blank separator for this page
                    sb.AppendLine();
                    continue;
                }

                // Non-empty page
                if (addPdfPageHeader)
                    sb.AppendLine($"=== [Page {i + 1}/{pageCount}] ===");

                sb.AppendLine(text.Trim());
                sb.AppendLine();
            }
            finally
            {
                if (textPage != IntPtr.Zero)
                    PdfiumNative.FPDFText_ClosePage(textPage);
                if (page != IntPtr.Zero)
                    PdfiumNative.FPDF_ClosePage(page);
            }
        }

        statusCallback?.Invoke(pageCount - 1, 100);

        return new PdfLoadResult(sb.ToString(), pageCount);
    }

    // -------------------------------
    //  Per-page text extraction
    // -------------------------------

    /// <summary>
    /// Extracts raw UTF-16 text from a PDFium text page and returns it as a
    /// managed <see cref="string"/>.
    ///
    /// This method is intentionally allocation-minimized: the caller supplies
    /// a reusable UTF-16 buffer (<see cref="ushort"/> array) which is resized
    /// only when necessary. No <see cref="System.Text.Encoding"/> conversions
    /// are used; instead the UTF-16 units are copied directly into a managed
    /// string via <c>Utf16BufferToString</c>.
    /// </summary>
    /// <param name="textPage">
    /// A PDFium text-page handle obtained from <c>FPDFText_LoadPage</c>.  
    /// Must be non-zero and valid for the duration of the call.
    /// </param>
    /// <param name="buffer">
    /// A reusable UTF-16 buffer used to store the raw text returned by
    /// <c>FPDFText_GetText</c>.  
    /// The buffer will be resized when its current capacity is insufficient
    /// to hold the extracted text (including the terminating NUL unit
    /// added by PDFium).  
    /// On return, this reference may point to a larger array if resizing
    /// was required.
    /// </param>
    /// <returns>
    /// A <see cref="string"/> containing the extracted UTF-16 text for the page.  
    /// If the page contains no text, or if PDFium returns zero characters,
    /// an empty string is returned.
    /// </returns>
    /// <remarks>
    /// <para>
    /// PDFium's <c>FPDFText_GetText</c> writes UTF-16 code units to the caller's
    /// buffer and usually appends a trailing NUL (U+0000).  
    /// This method strips that terminating unit before constructing the managed
    /// string, since .NET <see cref="string"/> does not require NUL termination.
    /// </para>
    /// <para>
    /// The returned text contains no page headers and no additional processing;
    /// it represents the raw textual content PDFium exposes for that page.
    /// Higher-level logic (page headers, reflow, paragraph merging, etc.) is
    /// applied by the layer above in <c>ExtractPages</c>.
    /// </para>
    /// </remarks>
    private static string ExtractPageText(IntPtr textPage, ref ushort[]? buffer)
    {
        var charCount = PdfiumNative.FPDFText_CountChars(textPage);
        if (charCount <= 0)
            return string.Empty;

        // ensure buffer capacity: charCount + 1 (for NUL)
        var required = charCount + 1;
        if (buffer == null || buffer.Length < required)
            buffer = new ushort[required];

        var written = PdfiumNative.FPDFText_GetText(textPage, 0, charCount, buffer);
        if (written <= 0)
            return string.Empty;

        var len = written;
        if (buffer[len - 1] == 0)
            len--;

        return Utf16BufferToString(buffer, len);
    }

    // -----------------------------------------------------------------------------
    // IgnoreUntrustedPdfText (object-level extraction)
    // -----------------------------------------------------------------------------

    private readonly struct TextObjItem
    {
        public readonly string Raw; // Original extracted text
        public readonly string Norm; // Normalized text used for repetition detection
        public readonly int YBucket; // Coarse Y-band bucket (page coordinates)

        public TextObjItem(string raw, string norm, int yBucket)
        {
            Raw = raw;
            Norm = norm;
            YBucket = yBucket;
        }
    }

    private static string ExtractPageText_IgnoreUntrusted(IntPtr page, IntPtr textPage)
    {
        // PDFium page object type: text
        const int FPDF_PAGEOBJ_TEXT = 1;

        var objCount = PdfiumNative.FPDFPage_CountObjects(page);
        if (objCount <= 0)
            return string.Empty;

        // Collect candidate text objects (cap initial capacity to avoid over-alloc on huge pages)
        var items = new List<TextObjItem>(Math.Min(objCount, 2048));

        for (var i = 0; i < objCount; i++)
        {
            var obj = PdfiumNative.FPDFPage_GetObject(page, i);
            if (obj == IntPtr.Zero)
                continue;

            if (PdfiumNative.FPDFPageObj_GetType(obj) != FPDF_PAGEOBJ_TEXT)
                continue;

            var raw = GetTextFromTextObject(obj, textPage);
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            // Bucket by Y position so repeated overlay text (tiled) becomes obvious.
            var yBucket = TryGetYBucket(obj, out var bucket) ? bucket : 0;

            var norm = NormalizeWhitespace(raw);
            if (norm.Length == 0)
                continue;

            items.Add(new TextObjItem(raw, norm, yBucket));
        }

        if (items.Count == 0)
            return string.Empty;

        // Count repetitions by (normalized text, Y-band). Overlay/watermark text tends to repeat.
        var freq = new Dictionary<(string Norm, int YBucket), int>(items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            var it = items[i];
            var key = (it.Norm, it.YBucket);

            if (freq.TryGetValue(key, out var count))
                freq[key] = count + 1;
            else
                freq[key] = 1;
        }

        // ---------- (1) Original --------------
        // var sb = new StringBuilder();
        //
        // for (var i = 0; i < items.Count; i++)
        // {
        //     var it = items[i];
        //     var repeats = freq[(it.Norm, it.YBucket)];
        //
        //     if (IsUntrustedOverlay(it.Norm, repeats))
        //         continue;
        //
        //     sb.Append(it.Raw);
        // }

        // -------- (2) Custom: Add newline --------
        var sb = new StringBuilder();
        int? lastBucket = null;

        // LineBucketGap is a tolerance band between adjacent text objects.
        // Too small => punctuation/quotes cause artificial line breaks (fragmentation).
        // Too large => merges distinct lines (run-on text).
        // Empirically, 2 provides good stability for novel PDFs while preserving line separation.
        const int LineBucketGap = 3;
        
        for (var i = 0; i < items.Count; i++)
        {
            var it = items[i];
            freq.TryGetValue((it.Norm, it.YBucket), out var repeats);

            if (IsUntrustedOverlay(it.Norm, repeats))
                continue;

            if (it.Raw.Length == 0)
                continue;

            // Only insert a newline when we clearly moved to another text line.
            // NOTE:
            // PDF text objects' bounding boxes can vary by glyph (quotes/punctuation),
            // so using strict bucket equality can create artificial line splits.
            // We treat a line as a Y-band (quantized yMid) and require a bucket gap
            // (>= 2) to absorb small Y drift and keep output reflow-friendly.
            if (lastBucket != null && Math.Abs(it.YBucket - lastBucket.Value) >= LineBucketGap)
                sb.AppendLine();

            sb.Append(it.Raw);
            lastBucket = it.YBucket;
        }

        return sb.ToString();

        // Local helper keeps the core loop readable.
        static bool TryGetYBucket(IntPtr obj, out int bucket)
        {
            bucket = 0;

            var ok = PdfiumNative.FPDFPageObj_GetBounds(
                obj, out _, out var bottom, out _, out var top) != 0;
            if (!ok)
                return false;

            var yMid = (bottom + top) * 0.5f;
            bucket = BucketY(yMid);
            // bucket = BucketY(bottom);
            return true;
        }
    }

    private static string GetTextFromTextObject(IntPtr textObj, IntPtr textPage)
    {
        // First pass: query required buffer size in BYTES (UTF-16LE, includes trailing NUL).
        var requiredBytes = PdfiumNative.FPDFTextObj_GetText(textObj, textPage, null, 0);
        if (requiredBytes == 0)
            return string.Empty;

        // Convert bytes -> ushort count. (+1) guards odd values.
        var u16Count = (int)((requiredBytes + 1) / 2);
        if (u16Count <= 1 || u16Count > 10_000_000)
            return string.Empty;

        var buf = new ushort[u16Count];

        // Second pass: fill buffer. Parameter is buflen in BYTES.
        var writtenBytes = PdfiumNative.FPDFTextObj_GetText(textObj, textPage, buf, requiredBytes);
        if (writtenBytes == 0)
            return string.Empty;

        var writtenU16 = (int)(writtenBytes / 2);
        if (writtenU16 <= 0)
            return string.Empty;

        // Trim trailing NUL if present.
        var len = writtenU16;
        if (buf[len - 1] == 0)
            len--;

        if (len <= 0)
            return string.Empty;

        return Utf16BufferToString(buf, len);
    }

    private static int BucketY(float yMid)
    {
        const float YBandStep = 5f;
        // Group nearby text objects into coarse horizontal bands to detect tiled overlays.
        // return (int)MathF.Round(yMid / 5f);
        return (int)MathF.Floor(yMid / YBandStep);
    }

    private static string NormalizeWhitespace(string s)
    {
        // Collapse consecutive whitespace to a single space, then trim.
        if (string.IsNullOrEmpty(s))
            return string.Empty;

        var sb = new StringBuilder(s.Length);
        var lastWasWs = false;

        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];

            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasWs)
                {
                    sb.Append(' ');
                    lastWasWs = true;
                }

                continue;
            }

            sb.Append(ch);
            lastWasWs = false;
        }

        return sb.ToString().Trim();
    }

    private static bool IsUntrustedOverlay(string norm, int repeatCount)
    {
        // Strong signal: the same normalized text repeats many times at the same Y-band.
        if (repeatCount >= 4 && norm.Length <= 200)
            return true;

        // Extra signal: "X X X X X ..." (single token repeated across the line).
        // This catches typical tiled watermark patterns.
        var parts = norm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 6)
            return false;

        var first = parts[0];
        var same = 1;

        for (var i = 1; i < parts.Length; i++)
        {
            if (parts[i] == first)
                same++;
        }

        // Almost all tokens are identical, and the token is short -> likely overlay text.
        return same >= parts.Length - 1 && first.Length <= 32;
    }

    // -------------------------------
    //  UTF-16 buffer → string helper
    // -------------------------------

    // private static string Utf16BufferToString(ushort[] buffer, int length)
    // {
    //     if (buffer == null)
    //         throw new ArgumentNullException(nameof(buffer));
    //     if ((uint)length > (uint)buffer.Length)
    //         throw new ArgumentOutOfRangeException(nameof(length));
    //     if (length == 0)
    //         return string.Empty;
    //
    //     // Reinterpret ushort[] as char[] and build string directly.
    //     // Requires .NET Standard 2.1+ / .NET Core 3+ / .NET 5+ (string(ReadOnlySpan<char>)).
    //     ReadOnlySpan<char> span = MemoryMarshal.Cast<ushort, char>(
    //         buffer.AsSpan(0, length));
    //
    //     return new string(span);
    // }

    // -------------------------------
    //  UTF-16 buffer → string helper
    // -------------------------------

    /// <summary>
    /// Creates a managed <see cref="string"/> directly from a UTF-16 buffer
    /// (<see cref="ushort"/> array) without using <see cref="System.Text.Encoding"/>.
    ///
    /// This helper is used by PDFium text extraction, which returns raw UTF-16
    /// code units (including a terminating NUL). By bypassing
    /// <see cref="System.Text.Encoding.Unicode"/>, we avoid the overhead of an
    /// allocator-bound decode step and instead copy the UTF-16 units directly
    /// into a managed string.
    /// </summary>
    /// <param name="buffer">
    /// A UTF-16 buffer containing <paramref name="length"/> consecutive UTF-16
    /// code units. The buffer must not be <c>null</c>.
    /// </param>
    /// <param name="length">
    /// Number of UTF-16 code units to copy into the resulting string.
    /// Must not exceed the buffer length.
    /// </param>
    /// <returns>
    /// A new <see cref="string"/> constructed directly from the UTF-16 units.
    /// If <paramref name="length"/> is zero, an empty string is returned.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="buffer"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="length"/> is greater than the size of the
    /// provided buffer.
    /// </exception>
    /// <remarks>
    /// <para><b>Why this method is marked <c>unsafe</c>:</b></para>
    /// <para>
    /// The method uses the pointer-based string constructor:
    /// <c>new string(char* ptr, int startIndex, int length)</c>.  
    /// It is extremely efficient but requires a raw pointer, so C# requires the
    /// method to be <c>unsafe</c>. The pointer is not used for arithmetic and the
    /// runtime still performs bounds checks, making the operation safe in
    /// practice as long as inputs are valid.
    /// </para>
    /// 
    /// <para><b>Why use this instead of <see cref="System.Text.Encoding"/>?</b></para>
    /// <list type="bullet">
    ///   <item><description>
    ///     PDFium already provides UTF-16 output, so decoding again via
    ///     <c>Encoding.Unicode</c> would perform redundant work.
    ///   </description></item>
    ///   <item><description>
    ///     This approach avoids extra allocations and buffer copies, improving
    ///     performance when extracting large PDFs.
    ///   </description></item>
    ///   <item><description>
    ///     No transcoding is needed — the bytes are already valid UTF-16.
    ///   </description></item>
    /// </list>
    /// 
    /// <para>
    /// The method simply reinterprets each <see cref="ushort"/> in the buffer
    /// as a UTF-16 <see cref="char"/> and produces a string of the requested
    /// length. The final string is fully managed and requires no pinning.
    /// </para>
    /// </remarks>
    private static unsafe string Utf16BufferToString(ushort[] buffer, int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if ((uint)length > (uint)buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(length));
        if (length == 0)
            return string.Empty;

        fixed (ushort* p = buffer)
        {
            // reinterpret UTF-16 units directly as chars
            return new string((char*)p, 0, length);
        }
    }
}