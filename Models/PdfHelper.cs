using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace OpenccNetLibGui.Models
{
    /// <summary>
    /// Specifies which PDF text extraction engine to use.
    /// </summary>
    public enum PdfEngine
    {
        /// <summary>
        /// Uses the PdfPig backend for text extraction.
        /// Suitable for general-purpose parsing and stable for
        /// most text-embedded PDFs.  
        /// Pure managed code, no native dependencies.
        /// </summary>
        PdfPig = 1,

        /// <summary>
        /// Uses the PDFium backend for text extraction.
        /// Faster and more robust against complex page structures,
        /// vector overlays, rotated text, or unusual PDF layouts.  
        /// Requires native PDFium runtime libraries.
        /// </summary>
        Pdfium = 2
    }

    /// <summary>
    /// Result of loading and optionally reflowing a PDF document.
    /// This is a pure data record and does not depend on any UI state.
    /// </summary>
    public sealed record PdfLoadResult(
        string Text,
        PdfEngine EngineUsed,
        bool AutoReflowApplied,
        int PageCount
    );
    
    internal readonly record struct PdfExtractResult(
        string Text,
        int PageCount
    );

    public static class PdfEngineExtensions
    {
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