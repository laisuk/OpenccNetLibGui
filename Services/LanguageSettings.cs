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
    public int AddPdfPageHeader { get; set; }
    public int CompactPdfText { get; set; }
    public int AutoReflowPdfText { get; set; }

    public int PdfEngine { get; set; }

    // NEW: preferred config shape
    public ShortHeadingSettings ShortHeadingSettings { get; set; } = ShortHeadingSettings.Default;

    // LEGACY: keep for loading older JSON (optional but recommended)
    public int ShortHeadingMaxLen { get; set; } = 8;

    /// <summary>
    /// Call after deserialization. Migrates legacy ShortHeadingMaxLen into ShortHeading when needed.
    /// </summary>
    public void Normalize()
    {
        // If new JSON doesn't exist but legacy exists, migrate it once.
        if (ShortHeadingSettings.MaxLen == ShortHeadingSettings.Default.MaxLen
            && ShortHeadingMaxLen != ShortHeadingSettings.Default.MaxLen)
        {
            ShortHeadingSettings.MaxLen = ShortHeadingMaxLen;
        }

        // Keep legacy in sync (so any old code path still behaves)
        ShortHeadingMaxLen = ShortHeadingSettings.MaxLen;
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

    public bool AllCjk { get; set; } = true;
    public bool AllAscii { get; set; }
    public bool AllAsciiDigits { get; set; } = true;
    public bool MixedCjkAscii { get; set; }

    public static ShortHeadingSettings Default => new()
    {
        MaxLen = 8,
        AllCjk = true,
        AllAscii = true,
        AllAsciiDigits = true,
        MixedCjkAscii = false
    };
}