using System;
using System.Collections.Generic;

namespace OpenccNetLibGui.Services;

[Serializable]
public class LanguageSettings
{
    public List<Language>? Languages { get; set; }
    public int CharCheck { get; set; }
    public Dictionary<char, char>? PunctuationChars { get; set; }
    public List<string>? TextFileTypes { get; set; }
    public List<string>? OfficeFileTypes { get; set; }
    public string? Dictionary { get; set; }
    public int Locale { get; set; }
    public int Punctuation { get; set; }
    public int ConvertFilename { get; set; }

    // -------------------- NEW preferred shape --------------------
    public PdfOptions? PdfOptions { get; set; } = new();

    // -------------------- LEGACY fields (keep for old JSON) --------------------
    // (These match current flat JSON keys.)
    public int AddPdfPageHeader { get; set; }
    public int CompactPdfText { get; set; }
    public int AutoReflowPdfText { get; set; }
    public int PdfEngine { get; set; }

    // NEW (but currently top-level in existing JSON file)
    public ShortHeadingSettings ShortHeadingSettings { get; set; } = ShortHeadingSettings.Default;

    // LEGACY: keep for loading older JSON
    public int ShortHeadingMaxLen { get; set; } = 8;

    /// <summary>
    /// Call after deserialization.
    /// Ensures PdfOptions exists and migrates legacy top-level PDF settings into PdfOptions.
    /// </summary>
    public void Normalize()
    {
        // Ensure PdfOptions always exists
        PdfOptions ??= new PdfOptions();

        // ---- Migrate legacy → new PdfOptions if PdfOptions looks untouched/default ----
        // We treat "PdfOptions not explicitly configured" as: engine=0 and maxLen default etc.
        // (engine=0 is invalid, so it's a good "unset" marker)
        var pdfOptionsUnset = PdfOptions.PdfEngine == 0;

        if (pdfOptionsUnset)
        {
            PdfOptions.AddPdfPageHeader = AddPdfPageHeader;
            PdfOptions.CompactPdfText = CompactPdfText;
            PdfOptions.AutoReflowPdfText = AutoReflowPdfText;
            PdfOptions.PdfEngine = PdfEngine == 0 ? 1 : PdfEngine;

            // Prefer new ShortHeadingSettings if present, otherwise legacy maxLen
            var sh = ShortHeadingSettings;
            if (ShortHeadingMaxLen > 0)
                sh.MaxLen = ShortHeadingMaxLen;

            PdfOptions.ShortHeadingSettings = sh;
        }

        // ---- Normalize nested options ----
        PdfOptions.Normalize();

        // ---- Keep legacy fields synced (so any old code path still behaves) ----
        AddPdfPageHeader = PdfOptions.AddPdfPageHeader;
        CompactPdfText = PdfOptions.CompactPdfText;
        AutoReflowPdfText = PdfOptions.AutoReflowPdfText;
        PdfEngine = PdfOptions.PdfEngine;

        ShortHeadingSettings = PdfOptions.ShortHeadingSettings;
        ShortHeadingMaxLen = ShortHeadingSettings.MaxLen;
    }
}

[Serializable]
public sealed class PdfOptions
{
    public int AddPdfPageHeader { get; set; }
    public int CompactPdfText { get; set; }
    public int AutoReflowPdfText { get; set; } = 1;

    /// <summary>1 = PdfPig, 2 = Pdfium</summary>
    public int PdfEngine { get; set; } // 0 = "unset" marker for migration

    public ShortHeadingSettings ShortHeadingSettings { get; set; } = ShortHeadingSettings.Default;

    public void Normalize()
    {
        // clamp maxLen
        ShortHeadingSettings.MaxLen = Math.Clamp(ShortHeadingSettings.MaxLen, 3, 30);

        // ensure engine is sane
        if (PdfEngine != 1 && PdfEngine != 2)
            PdfEngine = 1;
    }
}

[Serializable]
public class Language
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public List<string>? Name { get; set; }
    public string? T2SContent { get; set; }
    public string? S2TContent { get; set; }
    public string? CustomContent { get; set; }
    public string? StdContent { get; set; }
    public string? ZhtwContent { get; set; }
    public string? HkContent { get; set; }
    public string? CbZhtwContent { get; set; }
    public string? CbPunctuationContent { get; set; }
    public List<string>? CustomOptions { get; set; }
}

[Serializable]
public sealed class ShortHeadingSettings
{
    public int MaxLen { get; set; } = 8;

    // JSON expects 0/1 flags
    public int AllCjk { get; set; } = 1;
    public int AllAscii { get; set; } = 1;
    public int AllAsciiDigits { get; set; } = 1;
    public int MixedCjkAscii { get; set; }

    // Convenience bool views (optional, but makes call-sites nice)
    public bool AllCjkEnabled => AllCjk > 0;
    public bool AllAsciiEnabled => AllAscii > 0;
    public bool AllAsciiDigitsEnabled => AllAsciiDigits > 0;
    public bool MixedCjkAsciiEnabled => MixedCjkAscii > 0;

    public static ShortHeadingSettings Default => new()
    {
        MaxLen = 8,
        AllCjk = 1,
        AllAscii = 1,
        AllAsciiDigits = 1,
        MixedCjkAscii = 0
    };
}