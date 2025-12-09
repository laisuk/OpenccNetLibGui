using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenccNetLibGui.Models;

public static class PdfiumModel
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
    /// <returns>
    /// The concatenated plain-text content of all pages in the PDF, with page
    /// markers inserted in the form <c>=== [Page X/Y] ===</c>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="pdfPath"/> is <c>null</c>, empty,
    /// or whitespace-only.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when PDFium fails to load the document
    /// (i.e. <c>FPDF_LoadDocument</c> returns <c>NULL</c>).
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method is a thin, synchronous wrapper around the internal
    /// PDFium-based extraction pipeline and always behaves as if
    /// <paramref name=".addPdfPageHeader"/> were <c>true</c>, so that each page
    /// is prefixed with a page header marker.
    /// </para>
    /// <para>
    /// Only text-embedded PDF files are supported. Scanned/image-only PDFs
    /// will not yield useful results unless they have already been OCR-processed
    /// into selectable text.
    /// </para>
    /// </remarks>
    public static string ExtractText(string pdfPath)
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
                    addPdfPageHeader: true, // behavior same as before
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
    /// with optional page headers and progress reporting.
    /// </summary>
    /// <param name="pdfPath">
    /// Full path to the PDF file to open.
    /// </param>
    /// <param name="addPdfPageHeader">
    /// If <c>true</c>, each page is prefixed with a header marker in the form
    /// <c>=== [Page X/Y] ===</c>, matching the behavior of the PdfPig-based
    /// extractor and aiding downstream reflow or debugging.
    /// </param>
    /// <param name="statusCallback">
    /// Optional callback invoked periodically with human-readable progress
    /// messages such as <c>"Loading PDF [#####-----] 45%"</c>.  
    /// The callback is driven by an internal <c>Progress(pageIndex, percent)</c>
    /// delegate, which is converted into a text status string using
    /// <see cref="BuildProgressBar(int,int)"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// Token that can be used to cancel the operation.  
    /// If cancellation is requested, an <see cref="OperationCanceledException"/>
    /// is thrown.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation, whose result is the
    /// concatenated plain-text content of all pages in the PDF.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The extraction work is executed on a background thread using
    /// <see cref="Task.Run{TResult}(Func{TResult}, CancellationToken)"/>,
    /// making this method suitable for UI applications that must remain
    /// responsive while processing large PDFs.
    /// </para>
    /// <para>
    /// Only text-embedded PDFs are supported. Scanned/image-only PDFs will
    /// not yield meaningful text unless OCR has already produced selectable
    /// text content.
    /// </para>
    /// </remarks>
    public static Task<string> ExtractTextAsync(
        string pdfPath,
        bool addPdfPageHeader,
        Action<string>? statusCallback = null,
        CancellationToken cancellationToken = default)
    {
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
                    // Wrap your old status string into a simple progress callback (pageIndex, percent)
                    void Progress(int pageIndex, int percent)
                    {
                        if (statusCallback == null)
                            return;

                        var bar = BuildProgressBar(percent);
                        statusCallback($"Loading PDF {bar}  {percent}%");
                    }

                    return ExtractPages(
                        doc,
                        addPdfPageHeader,
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

    // -------------------------------
//  Shared core: page loop
// -------------------------------

    /// <summary>
    /// Shared PDFium-based page loop used by both synchronous and asynchronous
    /// extraction helpers.
    ///
    /// This method walks all pages in the given document, extracts their text,
    /// optionally prefixes each page with a header marker, and reports progress
    /// through a simple <c>(pageIndex, percent)</c> callback.
    /// </summary>
    /// <param name="doc">
    /// An open PDFium document handle obtained from
    /// <c>FPDF_LoadDocument</c>. Ownership of the handle remains with the caller;
    /// this method does not close the document.
    /// </param>
    /// <param name="addPdfPageHeader">
    /// If <c>true</c>, each page is prefixed with a header line in the form
    /// <c>=== [Page X/Y] ===</c>, where <c>X</c> is the 1-based page index and
    /// <c>Y</c> is the total page count.
    /// </param>
    /// <param name="statusCallback">
    /// Optional progress callback invoked during extraction with:
    /// <list type="bullet">
    ///   <item><description><c>pageIndex</c>: zero-based index of the page
    ///   currently being processed;</description></item>
    ///   <item><description><c>percent</c>: integer completion percentage
    ///   (1–100) based on pages processed.</description></item>
    /// </list>
    /// The callback is fired at the first page, the last page, and at
    /// adaptive intervals depending on total page count.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation. If cancellation is requested,
    /// an <see cref="OperationCanceledException"/> is thrown.
    /// </param>
    /// <returns>
    /// A single string containing the concatenated plain-text content of all
    /// successfully processed pages. Each page is separated by a blank line,
    /// and optional page headers are included when
    /// <paramref name="addPdfPageHeader"/> is <c>true</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method is the core engine behind <see cref="ExtractText(string)"/>
    /// and <see cref="ExtractTextAsync(string, bool, Action{string}?, CancellationToken)"/>.
    /// It allocates a reusable UTF-16 buffer once and passes it by reference to
    /// <c>ExtractPageText</c> to minimize per-page allocations.
    /// </para>
    /// <para>
    /// If <c>FPDF_GetPageCount</c> returns zero or a negative value, the method
    /// immediately reports <c>(pageIndex: 0, percent: 100)</c> to
    /// <paramref name="statusCallback"/> (if provided) and returns an empty
    /// string.
    /// </para>
    /// </remarks>
    private static string ExtractPages(
        IntPtr doc,
        bool addPdfPageHeader,
        Action<int, int>? statusCallback, // (pageIndex, percent)
        CancellationToken cancellationToken)
    {
        var pageCount = PdfiumNative.FPDF_GetPageCount(doc);
        if (pageCount <= 0)
        {
            statusCallback?.Invoke(0, 100);
            return string.Empty;
        }

        static int GetProgressBlock(int totalPages)
        {
            return totalPages switch
            {
                <= 20 => 1,
                <= 100 => 3,
                <= 300 => 5,
                _ => Math.Max(1, totalPages / 20)
            };
        }

        var block = GetProgressBlock(pageCount);
        var sb = new StringBuilder();

        // Reusable buffer to reduce allocations across pages
        ushort[]? buffer = null;

        for (var i = 0; i < pageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // progress (same logic as before, but callback is generalized)
            if (statusCallback != null &&
                (i % block == 0 || i == 0 || i == pageCount - 1))
            {
                var percent = (int)(((double)(i + 1) / pageCount) * 100);
                statusCallback(i, percent);
            }

            var page = IntPtr.Zero;
            var textPage = IntPtr.Zero;

            try
            {
                page = PdfiumNative.FPDF_LoadPage(doc, i);
                if (page == IntPtr.Zero)
                    continue;

                textPage = PdfiumNative.FPDFText_LoadPage(page);
                if (textPage == IntPtr.Zero)
                    continue;

                var text = ExtractPageText(textPage, ref buffer);
                // if (string.IsNullOrEmpty(text))
                //     continue;
                //
                // if (addPdfPageHeader)
                //     sb.AppendLine($"=== [Page {i + 1}/{pageCount}] ===");
                // 🔹 handle empty/blank pages explicitly
                if (string.IsNullOrWhiteSpace(text))
                {
                    if (addPdfPageHeader)
                    {
                        sb.AppendLine($"=== [Page {i + 1}/{pageCount}] ===");
                    }

                    // always emit at least one blank line for this empty page
                    sb.AppendLine(); // visible blank page separator

                    continue;
                }

                // 🔹 non-empty page
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

        return sb.ToString();
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

    // -------------------------------
    //  ProgressBar builder (unchanged)
    // -------------------------------
    private static string BuildProgressBar(int percent, int width = 10)
    {
        percent = Math.Clamp(percent, 0, 100);
        var filled = (int)((long)percent * width / 100);

        var sb = new StringBuilder(width * 4 + 2);
        sb.Append('[');

        for (var i = 0; i < filled; i++) sb.Append("🟩");
        for (var i = filled; i < width; i++) sb.Append("🟨");

        sb.Append(']');
        return sb.ToString();
    }
}