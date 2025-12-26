using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OpenccNetLibGui.Models;

/// <summary>
/// Helper utilities for selecting and initializing the effective
/// PDF text extraction engine at runtime.
/// </summary>
/// <remarks>
/// This helper encapsulates platform- and runtime-specific rules
/// for deciding whether the native PDFium engine can be used.
/// It ensures that callers always receive a valid, supported
/// <see cref="PdfEngine"/> value without needing to handle
/// platform detection themselves.
/// </remarks>
public static class PdfEngineHelper
{
    /// <summary>
    /// Set of runtime identifiers (RIDs) for which PDFium native
    /// binaries are known to be available and supported.
    /// </summary>
    /// <remarks>
    /// The RID format follows the standard <c>&lt;os&gt;-&lt;arch&gt;</c>
    /// convention used by .NET (e.g. <c>win-x64</c>, <c>osx-arm64</c>).
    /// If the current runtime is not listed here, the system
    /// automatically falls back to the managed PdfPig engine.
    /// </remarks>
    private static readonly HashSet<string> SupportedPdfiumRuntimes = new()
    {
        "win-x64",
        "linux-x64",
        "osx-x64",
        "osx-arm64",
        "win-x86"
    };

    /// <summary>
    /// Resolves the effective PDF extraction engine to use based on
    /// user preference and current runtime capabilities.
    /// </summary>
    /// <param name="requestedEngine">
    /// User-requested engine value, typically coming from persisted
    /// settings or a UI selection.
    /// </param>
    /// <returns>
    /// The engine that will actually be used at runtime:
    /// <list type="bullet">
    ///   <item><description>
    ///     <see cref="PdfEngine.PdfPig"/> if explicitly requested, or
    ///     if the current platform does not support PDFium.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="PdfEngine.Pdfium"/> if requested and native
    ///     PDFium binaries are available for the current runtime.
    ///   </description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// This method never throws due to platform incompatibility.
    /// If PDFium cannot be used on the current runtime, it
    /// transparently falls back to the managed PdfPig engine.
    /// </remarks>
    public static PdfEngine InitPdfEngine(int requestedEngine)
    {
        var selected = (PdfEngine)requestedEngine;

        // If user explicitly selected PdfPig → always honor it
        if (selected == PdfEngine.PdfPig)
            return PdfEngine.PdfPig;

        // Otherwise, user requested Pdfium → verify platform support
        var rid = GetCurrentRid();

        return SupportedPdfiumRuntimes.Contains(rid)
            ? PdfEngine.Pdfium // native PDFium available
            : PdfEngine.PdfPig; // fallback to managed engine
    }

    /// <summary>
    /// Computes the current runtime identifier (RID) in the form
    /// <c>&lt;os&gt;-&lt;arch&gt;</c>.
    /// </summary>
    /// <returns>
    /// A RID string such as <c>win-x64</c>, <c>linux-x64</c>,
    /// or <c>osx-arm64</c>. If the operating system or architecture
    /// is not recognized, <c>unknown</c> is used in the corresponding
    /// position.
    /// </returns>
    /// <remarks>
    /// This method relies on <see cref="OperatingSystem"/> and
    /// <see cref="RuntimeInformation"/> to detect the current
    /// platform at runtime. The returned value is intended for
    /// compatibility checks only and is not guaranteed to match
    /// the exact RID used during publishing.
    /// </remarks>
    private static string GetCurrentRid()
    {
        var os = OperatingSystem.IsWindows() ? "win" :
            OperatingSystem.IsLinux() ? "linux" :
            OperatingSystem.IsMacOS() ? "osx" :
            "unknown";

        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => "unknown"
        };

        return $"{os}-{arch}";
    }
}