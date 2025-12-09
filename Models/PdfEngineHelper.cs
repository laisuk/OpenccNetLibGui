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
        // User selected PdfPig
        if (requestedEngine == 1)
            return PdfEngine.PdfPig;

        // Determine current runtime
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

        var rid = $"{os}-{arch}";

        // Check if Pdfium supported
        return SupportedPdfiumRuntimes.Contains(rid) ? PdfEngine.Pdfium :
            // Fallback
            PdfEngine.PdfPig;
    }
}
