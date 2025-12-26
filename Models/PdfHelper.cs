using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace OpenccNetLibGui.Models
{
    /// <summary>
    /// Specifies which PDF text extraction backend is used.
    /// </summary>
    /// <remarks>
    /// Different engines have different trade-offs in terms of
    /// performance, robustness, and deployment requirements.
    /// The selected engine affects how text is extracted, but not
    /// how the extracted text is post-processed (reflow, conversion, etc.).
    /// </remarks>
    public enum PdfEngine
    {
        /// <summary>
        /// Uses the PdfPig backend for text extraction.
        /// </summary>
        /// <remarks>
        /// PdfPig is a pure managed (.NET) PDF parser with no native
        /// dependencies. It is suitable for most text-embedded PDFs
        /// and offers good stability and portability across platforms.
        ///
        /// This engine may struggle with PDFs that rely heavily on
        /// complex vector layouts, rotated glyphs, or layered text overlays.
        /// </remarks>
        PdfPig = 1,

        /// <summary>
        /// Uses the PDFium backend for text extraction.
        /// </summary>
        /// <remarks>
        /// PDFium is a native PDF rendering and parsing engine that
        /// generally provides more accurate text extraction for
        /// complex PDFs, including those with:
        /// <list type="bullet">
        ///   <item><description>rotated or transformed text</description></item>
        ///   <item><description>vector-based overlays</description></item>
        ///   <item><description>non-standard or fragmented text layout</description></item>
        /// </list>
        ///
        /// This engine requires native PDFium runtime libraries
        /// appropriate for the current platform.
        /// </remarks>
        Pdfium = 2
    }

    /// <summary>
    /// Represents the result of loading a PDF document and producing
    /// its extracted (and optionally reflowed) plain text content.
    /// </summary>
    /// <remarks>
    /// This type is a pure data container and does not depend on
    /// any UI state or threading context. It can be safely passed
    /// between layers (e.g. model → view-model).
    /// </remarks>
    /// <param name="Text">
    /// The extracted plain-text content of the PDF.
    /// </param>
    /// <param name="EngineUsed">
    /// The PDF extraction engine that was actually used to produce
    /// the text content.
    /// </param>
    /// <param name="AutoReflowApplied">
    /// Indicates whether automatic CJK paragraph reflow or other
    /// structural post-processing was applied to the extracted text.
    /// </param>
    /// <param name="PageCount">
    /// Total number of pages in the source PDF document.
    /// </param>
    public sealed record PdfLoadResult(
        string Text,
        PdfEngine EngineUsed,
        bool AutoReflowApplied,
        int PageCount
    );

    /// <summary>
    /// Lightweight result produced by the low-level PDF extraction
    /// stage before any reflow or higher-level processing is applied.
    /// </summary>
    /// <remarks>
    /// This type is internal by design and used to separate raw
    /// extraction concerns (PDFium/PdfPig) from downstream logic
    /// such as reflow, conversion, or UI presentation.
    /// </remarks>
    /// <param name="Text">
    /// Raw extracted text content.
    /// </param>
    /// <param name="PageCount">
    /// Number of pages successfully processed during extraction.
    /// </param>
    internal readonly record struct PdfExtractResult(
        string Text,
        int PageCount
    );

    /// <summary>
    /// Extension helpers for <see cref="PdfEngine"/>.
    /// </summary>
    public static class PdfEngineExtensions
    {
        /// <summary>
        /// Returns a user-facing display name for the PDF engine.
        /// </summary>
        /// <param name="engine">
        /// The PDF extraction engine value.
        /// </param>
        /// <returns>
        /// A human-readable name suitable for UI display
        /// (e.g. dropdowns, status bars, logs).
        /// </returns>
        public static string ToDisplayName(this PdfEngine engine)
        {
            return engine switch
            {
                PdfEngine.PdfPig => "PdfPig [managed]",
                PdfEngine.Pdfium => "Pdfium [native]",
                _ => engine.ToString()
            };
        }
    }

    internal static class PdfHelper
    {
        /// <summary>
        /// Determines whether a file is a PDF by examining both its
        /// extension and its file header (magic bytes).
        /// </summary>
        /// <param name="filePath">Path to the file to examine.</param>
        /// <returns>
        /// <c>true</c> if the file appears to be a valid PDF; otherwise <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method does not fully parse the PDF. It performs a lightweight
        /// signature check by looking for the <c>%PDF-</c> header within the
        /// first kilobyte of the file, which is required by the PDF specification.
        ///
        /// This approach is significantly more reliable than extension-only
        /// checks and avoids loading native PDF libraries.
        /// </remarks>
        public static bool IsPdf(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            if (!File.Exists(filePath))
                return false;

            // Optional fast hint (not authoritative)
            var ext = Path.GetExtension(filePath);
            var extensionLooksPdf =
                ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

            try
            {
                using var fs = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);

                if (!extensionLooksPdf && fs.Length < 5)
                    return false;

                // PDF header must appear within the first 1024 bytes
                var readLen = (int)Math.Min(1024, fs.Length);
                Span<byte> buffer = stackalloc byte[readLen];
                fs.ReadExactly(buffer);

                // Look for ASCII "%PDF-" marker
                for (var i = 0; i <= readLen - 5; i++)
                {
                    if (buffer[i] == (byte)'%' &&
                        buffer[i + 1] == (byte)'P' &&
                        buffer[i + 2] == (byte)'D' &&
                        buffer[i + 3] == (byte)'F' &&
                        buffer[i + 4] == (byte)'-')
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                // I/O error, permission issue, etc.
                return false;
            }
        }

        /// <summary>
        /// Asynchronously loads a PDF file and extracts plain text from all pages,
        /// with optional page headers and real-time progress reporting.
        ///
        /// This method runs the extraction work on a background thread using
        /// <see cref="Task.Run{TResult}(Func{TResult}, CancellationToken)"/> and is suitable
        /// for UI applications that must remain responsive while processing
        /// large PDFs.
        ///
        /// Extraction uses PdfPig’s <see cref="ContentOrderTextExtractor"/> to obtain
        /// text in a visually ordered manner. No layout information (fonts,
        /// positions, spacing) is preserved—only text content.
        /// </summary>
        /// <param name="filename">
        /// Full path to the PDF file to load.
        /// </param>
        /// <param name="addPdfPageHeader">
        /// If <c>true</c>, each extracted page is prefixed with a marker in the
        /// form <c>=== [Page X/Y] ===</c>, which is useful for debugging or for
        /// downstream paragraph-reflow heuristics that rely on page boundaries.
        /// </param>
        /// <param name="progressCallback">
        /// Optional callback invoked periodically progress as percent.  
        ///  
        /// The callback is triggered:
        /// <list type="bullet">
        ///   <item><description>at page 1,</description></item>
        ///   <item><description>at the last page,</description></item>
        ///   <item><description>and every adaptive interval determined by the
        ///     total page count.</description></item>
        /// </list>
        /// This is typically used to update a UI status bar.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.  
        /// If cancellation is requested, a <see cref="OperationCanceledException"/>
        /// is thrown immediately.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation, returning the fully
        /// concatenated plain-text content of the PDF file.
        ///
        /// Line breaks are normalized, trailing whitespace is trimmed per page,
        /// and an empty string is returned for PDFs with zero pages.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The adaptive progress interval scales based on page count:
        /// <list type="bullet">
        ///   <item><description>≤ 20 pages → update every page</description></item>
        ///   <item><description>≤ 100 pages → every 3 pages</description></item>
        ///   <item><description>≤ 300 pages → every 5 pages</description></item>
        ///   <item><description>&gt; 300 pages → ~5% of total pages</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Because the underlying extraction is CPU-bound and potentially slow,
        /// this method should always be awaited to prevent blocking the caller's
        /// thread.
        /// </para>
        /// </remarks>
        internal static Task<PdfExtractResult> LoadPdfTextAsync(
            string filename,
            bool addPdfPageHeader,
            Action<int>? progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("PDF path is required.", nameof(filename));

            return Task.Run(() =>
            {
                using var document = PdfDocument.Open(filename);

                var sb = new StringBuilder();
                var total = document.NumberOfPages;

                if (total <= 0)
                {
                    progressCallback?.Invoke(0);
                    // return string.Empty;
                    return new PdfExtractResult(
                        string.Empty,
                        0
                    );
                }

                // Adaptive progress update interval
                static int GetProgressBlock(int totalPages)
                {
                    return totalPages switch
                    {
                        <= 20 => 1,
                        <= 100 => 3,
                        <= 300 => 5,
                        _ => Math.Max(1, totalPages / 20)
                    };

                    // large PDFs: ~5% intervals
                }

                var block = GetProgressBlock(total);

                for (var i = 1; i <= total; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Progress callback
                    if (progressCallback != null && (i % block == 0 || i == 1 || i == total))
                    {
                        var percent = (int)((double)i / total * 100);
                        progressCallback(percent);
                    }


                    var page = document.GetPage(i);
                    var text = ContentOrderTextExtractor.GetText(page);

                    text = text.Trim('\r', '\n', ' ');

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        if (addPdfPageHeader)
                            sb.AppendLine($"=== [Page {i}/{total}] ===");

                        sb.AppendLine(); // visible blank page separator
                        continue;
                    }

                    if (addPdfPageHeader)
                        sb.AppendLine($"=== [Page {i}/{total}] ===");

                    sb.AppendLine(text);
                    sb.AppendLine();
                }

                // return sb.ToString();
                return new PdfExtractResult(
                    sb.ToString(),
                    total
                );
            }, cancellationToken);
        }
    }
}