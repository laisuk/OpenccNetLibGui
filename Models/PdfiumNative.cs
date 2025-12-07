// ReSharper disable InconsistentNaming

using System;
using System.Runtime.InteropServices;

namespace OpenccNetLibGui.Models;

internal static class PdfiumNative
{
    private const string DllName = "pdfium";

    // PDFium uses __stdcall on Windows (FPDF_CALLCONV)
    private const CallingConvention CallConv = CallingConvention.StdCall;

    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern void FPDF_InitLibrary();

    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern void FPDF_DestroyLibrary();

    [DllImport(DllName, CallingConvention = CallConv, CharSet = CharSet.Ansi)]
    public static extern IntPtr FPDF_LoadDocument(
        [MarshalAs(UnmanagedType.LPStr)] string file_path,
        [MarshalAs(UnmanagedType.LPStr)] string? password);

    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern void FPDF_CloseDocument(IntPtr document);

    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern int FPDF_GetPageCount(IntPtr document);

    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern IntPtr FPDF_LoadPage(IntPtr document, int page_index);

    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern void FPDF_ClosePage(IntPtr page);

    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern IntPtr FPDFText_LoadPage(IntPtr page);

    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern void FPDFText_ClosePage(IntPtr text_page);

    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern int FPDFText_CountChars(IntPtr text_page);

    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern int FPDFText_GetText(
        IntPtr text_page,
        int start_index,
        int count,
        [Out] ushort[]? result);

    [DllImport(DllName, CallingConvention = CallConv)]
    public static extern IntPtr FPDF_LoadMemDocument(
        byte[] data,
        int size,
        [MarshalAs(UnmanagedType.LPStr)] string? password);
}