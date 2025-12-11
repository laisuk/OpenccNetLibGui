using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OpenccNetLibGui.Models;

public static class PdfEngineHelper
{
    private static readonly HashSet<string> SupportedPdfiumRuntimes = new()
    {
        "win-x64",
        "linux-x64",
        "osx-x64",
        "osx-arm64",
        "win-x86"
    };

    public static PdfEngine InitPdfEngine(int requestedEngine)
    {
        var selected = (PdfEngine)requestedEngine;

        // If user selected PdfPig → always return PdfPig
        if (selected == PdfEngine.PdfPig)
            return PdfEngine.PdfPig;

        // Otherwise user selected Pdfium → check platform
        var rid = GetCurrentRid();

        return SupportedPdfiumRuntimes.Contains(rid)
            ? PdfEngine.Pdfium     // success
            : PdfEngine.PdfPig;    // fallback
    }

    private static string GetCurrentRid()
    {
        var os = OperatingSystem.IsWindows() ? "win" :
            OperatingSystem.IsLinux()   ? "linux" :
            OperatingSystem.IsMacOS()   ? "osx" : "unknown";

        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64  => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86   => "x86",
            _ => "unknown"
        };

        return $"{os}-{arch}";
    }
}

