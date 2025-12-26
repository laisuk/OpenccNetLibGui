// ReSharper disable InconsistentNaming

using System;
using System.Runtime.InteropServices;

namespace OpenccNetLibGui.Models;

/// <summary>
/// Minimal P/Invoke bindings for a small subset of PDFium (FPDF_ / FPDFText_).
/// </summary>
/// <remarks>
/// ⚠️ This is a low-level interop layer:
/// - All <see cref="IntPtr"/> values returned by PDFium are *opaque handles* managed by the native library.
/// - Handles must be closed via the corresponding Close function to avoid leaks.
/// - Most PDFium functions do not throw; failures are typically signaled by returning null/0.
/// 
/// Calling convention:
/// - PDFium uses <c>FPDF_CALLCONV</c>, which is <see cref="CallingConvention.StdCall"/> on Windows builds.
///   Using the wrong calling convention can cause stack imbalance and crashes.
/// </remarks>
internal static class PdfiumNative
{
    /// <summary>
    /// The native library name. The runtime resolves to:
    /// - Windows: pdfium.dll
    /// - Linux: libpdfium.so
    /// - macOS: libpdfium.dylib
    /// </summary>
    private const string DllName = "pdfium";

    /// <summary>
    /// PDFium uses __stdcall on Windows (FPDF_CALLCONV). Keep this consistent across all imports.
    /// </summary>
    private const CallingConvention CallConv = CallingConvention.StdCall;

    /// <summary>
    /// Initializes PDFium global state for the current process.
    /// </summary>
    /// <remarks>
    /// Must be called once (typically at application start) before using most other PDFium APIs.
    /// Call <see cref="FPDF_DestroyLibrary"/> when the process is done with PDFium.
    /// </remarks>
    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern void FPDF_InitLibrary();

    /// <summary>
    /// Releases PDFium global state for the current process.
    /// </summary>
    /// <remarks>
    /// After calling this, any existing PDFium document/page/text-page handles become invalid.
    /// </remarks>
    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern void FPDF_DestroyLibrary();

    /// <summary>
    /// Opens a PDF document from a file path.
    /// </summary>
    /// <param name="file_path">
    /// Path to a PDF file. PDFium’s C API expects a narrow <c>char*</c> string.
    /// </param>
    /// <param name="password">
    /// Optional password for encrypted documents; pass null if not needed.
    /// </param>
    /// <returns>
    /// An opaque document handle on success; <see cref="IntPtr.Zero"/> on failure.
    /// </returns>
    /// <remarks>
    /// Lifetime:
    /// - The returned handle must be closed with <see cref="FPDF_CloseDocument"/>.
    ///
    /// Encoding note:
    /// - This binding uses ANSI marshaling (<see cref="CharSet.Ansi"/> / LPStr) to match the C API.
    ///   On Windows, non-ASCII paths can be problematic depending on the PDFium build.
    ///   If you need full Unicode path support, consider opening the file yourself and using
    ///   <see cref="FPDF_LoadMemDocument"/> (or a wide-path variant if your PDFium build provides one).
    /// </remarks>
    // NOTE: PDFium's C API takes const char* for file_path/password (no wchar_t* variant in stock API).
    // Do NOT change to LPWStr. For full Unicode path safety on Windows, load bytes yourself and use
    // FPDF_LoadMemDocument instead.
    [DllImport(DllName, CallingConvention = CallConv, CharSet = CharSet.Ansi)]
    public static extern IntPtr FPDF_LoadDocument(
        [MarshalAs(UnmanagedType.LPStr)] string file_path,
        [MarshalAs(UnmanagedType.LPStr)] string? password);

    /// <summary>
    /// Closes a document handle returned by <see cref="FPDF_LoadDocument"/> or <see cref="FPDF_LoadMemDocument"/>.
    /// </summary>
    /// <param name="document">Opaque document handle. Safe to pass <see cref="IntPtr.Zero"/> (no-op is typical).</param>
    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern void FPDF_CloseDocument(IntPtr document);

    /// <summary>
    /// Returns the number of pages in a document.
    /// </summary>
    /// <param name="document">Opaque document handle.</param>
    /// <returns>The page count, or 0 on failure (including invalid handle).</returns>
    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern int FPDF_GetPageCount(IntPtr document);

    /// <summary>
    /// Loads a page from a document.
    /// </summary>
    /// <param name="document">Opaque document handle.</param>
    /// <param name="page_index">Zero-based page index.</param>
    /// <returns>An opaque page handle on success; <see cref="IntPtr.Zero"/> on failure.</returns>
    /// <remarks>
    /// The returned page handle must be closed with <see cref="FPDF_ClosePage"/>.
    /// </remarks>
    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern IntPtr FPDF_LoadPage(IntPtr document, int page_index);

    /// <summary>
    /// Closes a page handle returned by <see cref="FPDF_LoadPage"/>.
    /// </summary>
    /// <param name="page">Opaque page handle.</param>
    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern void FPDF_ClosePage(IntPtr page);

    /// <summary>
    /// Creates a text-page handle used for text extraction from a loaded page.
    /// </summary>
    /// <param name="page">Opaque page handle.</param>
    /// <returns>An opaque text-page handle on success; <see cref="IntPtr.Zero"/> on failure.</returns>
    /// <remarks>
    /// The returned handle must be closed with <see cref="FPDFText_ClosePage"/>.
    /// </remarks>
    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern IntPtr FPDFText_LoadPage(IntPtr page);

    /// <summary>
    /// Closes a text-page handle returned by <see cref="FPDFText_LoadPage"/>.
    /// </summary>
    /// <param name="text_page">Opaque text-page handle.</param>
    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern void FPDFText_ClosePage(IntPtr text_page);

    /// <summary>
    /// Returns the number of UTF-16 code units (characters) available in the text-page.
    /// </summary>
    /// <param name="text_page">Opaque text-page handle.</param>
    /// <returns>The character count, or 0 on failure.</returns>
    /// <remarks>
    /// This count is used to size buffers passed to <see cref="FPDFText_GetText"/>.
    /// </remarks>
    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern int FPDFText_CountChars(IntPtr text_page);

    /// <summary>
    /// Extracts text from a text-page into a UTF-16 buffer.
    /// </summary>
    /// <param name="text_page">Opaque text-page handle.</param>
    /// <param name="start_index">Start index (0-based) into the text stream.</param>
    /// <param name="count">
    /// Number of characters to extract. PDFium will write at most <paramref name="count"/> UTF-16 code units,
    /// typically including a terminating NUL when space permits.
    /// </param>
    /// <param name="result">
    /// Destination buffer of UTF-16 code units (ushort). Can be null to query the required size
    /// (common PDFium pattern in some APIs), but for this function you normally pass an allocated buffer.
    /// </param>
    /// <returns>
    /// The number of UTF-16 code units written into <paramref name="result"/> (often including the trailing NUL),
    /// or 0 on failure.
    /// </returns>
    /// <remarks>
    /// Buffer contract:
    /// - Allocate a <see cref="ushort"/> buffer large enough for the requested range.
    /// - For “get all text”, a common pattern is:
    ///   <c>n = FPDFText_CountChars(tp)</c>, allocate <c>ushort[n + 1]</c>, call GetText(tp, 0, n, buf).
    /// - Convert to .NET string via <see cref="System.Text.Encoding.Unicode"/> or by trimming the trailing NUL.
    /// </remarks>
    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern int FPDFText_GetText(
        IntPtr text_page,
        int start_index,
        int count,
        [Out] ushort[]? result);

    /// <summary>
    /// Opens a PDF document from an in-memory byte buffer.
    /// </summary>
    /// <param name="data">Raw PDF bytes.</param>
    /// <param name="size">Size in bytes (usually <c>data.Length</c>).</param>
    /// <param name="password">Optional password for encrypted documents.</param>
    /// <returns>An opaque document handle on success; <see cref="IntPtr.Zero"/> on failure.</returns>
    /// <remarks>
    /// ⚠️ Important lifetime rule:
    /// Some PDFium builds may keep pointers into the supplied <paramref name="data"/> buffer.
    /// To be safe, keep the <see cref="byte"/>[] referenced (rooted) and unchanged for the
    /// entire lifetime of the returned document handle (until <see cref="FPDF_CloseDocument"/>).
    /// A simple approach is to store the byte[] alongside the document handle.
    /// </remarks>
    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern IntPtr FPDF_LoadMemDocument(
        byte[] data,
        int size,
        [MarshalAs(UnmanagedType.LPStr)] string? password);
}