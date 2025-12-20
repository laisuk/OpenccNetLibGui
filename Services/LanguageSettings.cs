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
    public PdfOptions PdfOptions { get; set; } = new();
}

[Serializable]
public sealed class PdfOptions
{
    public int AddPdfPageHeader { get; set; }
    public int CompactPdfText { get; set; }
    public int AutoReflowPdfText { get; set; } = 1;

    /// <summary>1 = PdfPig, 2 = Pdfium</summary>
    public int PdfEngine { get; set; } = 2; // Default = Pdfium

    public ShortHeadingSettings ShortHeadingSettings { get; set; } = ShortHeadingSettings.Default;
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
        MixedCjkAscii = 1
    };
}