using System;
using System.IO;

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
    /// <param name="PageCount">
    /// Total number of pages in the source PDF document.
    /// </param>
    public sealed record PdfLoadResult(
        string Text,
        // PdfEngine EngineUsed,
        // bool AutoReflowApplied,
        int PageCount
    );

    /// <summary>
    /// ViewModel-level result representing the final PDF text
    /// after optional reflow and UI-specific processing.
    /// </summary>
    public sealed record PdfVmResult(
        string Text,
        PdfEngine EngineUsed,
        bool AutoReflowApplied,
        int PageCount
    );
}