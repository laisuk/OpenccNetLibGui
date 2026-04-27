using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

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
    public string ThemeMode { get; set; } = "System";
    public bool Punctuation { get; set; }
    public bool ConvertFilename { get; set; }

    // -------------------- NEW preferred shape --------------------
    public PdfOptions PdfOptions { get; set; } = new();
    public SentenceBoundaryModeSetting? SentenceBoundaryMode { get; set; } = new();
}

[Serializable]
public sealed class PdfOptions
{
    public bool AddPdfPageHeader { get; set; }
    public bool CompactPdfText { get; set; }
    public bool AutoReflowPdfText { get; set; } = true;
    public bool IgnoreUntrustedPdfText { get; set; }

    /// <summary>1 = PdfPig, 2 = Pdfium</summary>
    public int PdfEngine { get; set; } = 2; // Default = Pdfium

    public ShortHeadingSettings ShortHeadingSettings { get; set; } = ShortHeadingSettings.Default;
}

[Serializable]
public class Language
{
    public int Id { get; set; }
    public int Locale { get; set; }
    public string Code { get; set; } = "";
    public List<string> Name { get; set; } = new();
    public string T2SContent { get; set; } = "";
    public string S2TContent { get; set; } = "";
    public string CustomContent { get; set; } = "";
    public string UiLanguageContent { get; set; } = "UI Language";
    public string TabMainContent { get; set; } = "Main Conversion";
    public string TabBatchContent { get; set; } = "Batch Conversion";
    public string TabSettingsContent { get; set; } = "Settings";
    public string TabMessageContent { get; set; } = "Message";
    public string TabPreviewContent { get; set; } = "Preview";
    public string StdContent { get; set; } = "";
    public string ZhtwContent { get; set; } = "";
    public string HkContent { get; set; } = "";
    public string CbZhtwContent { get; set; } = "";
    public string CbPunctuationContent { get; set; } = "";
    public string BtnPasteContent { get; set; } = "Paste";
    public string BtnCopyContent { get; set; } = "Copy";
    public string BtnPreviewContent { get; set; } = "Preview";
    public string BtnDetectContent { get; set; } = "Detect";
    public string BtnOpenFileContent { get; set; } = "open File";
    public string BtnSaveAsContent { get; set; } = "Save As";
    public string UnsavedChangesContent { get; set; } = "Unsaved changes";
    public string AllSettingsSavedContent { get; set; } = "All settings saved";
    public string BtnSaveAdvancedSettingsContent { get; set; } = "Save Advanced Settings";
    public string ProcessContent { get; set; } = "Process";
    public string BatchStartContent { get; set; } = "Batch Start";
    public string SourceContent { get; set; } = "Source:";
    public string DestinationContent { get; set; } = "Destination:";
    public string OutputContent { get; set; } = "Output:";
    public string FilenameContent { get; set; } = "Filename";
    public string ConversionSettingsContent { get; set; } = "Conversion Settings";
    public string ConvertFilenameContent { get; set; } = "Convert filename";
    public string PdfOptionsContent { get; set; } = "PDF Options";
    public string AddPdfPageHeaderContent { get; set; } = "Add page header";
    public string CompactPdfTextContent { get; set; } = "Compact PDF text";
    public string AutoReflowPdfTextContent { get; set; } = "Auto-Reflow PDF text";
    public string IgnoreUntrustedPdfTextContent { get; set; } = "Ignore untrusted PDF text";
    public string PdfEngineContent { get; set; } = "PDF Engine";
    public string UsePdfPigEngineContent { get; set; } = "Use PdfPig engine";
    public string UsePdfiumEngineContent { get; set; } = "Use Pdfium (native) engine";
    public string HeadingRulesContent { get; set; } = "Heading Rules";
    public string ShortHeadingSettingsContent { get; set; } = "Short heading settings...";
    public string ThemeModeContent { get; set; } = "Theme Mode";
    public List<string> ThemeModeSelectionContent { get; set; } = new();
    public List<string> SaveTargetSelectionContent { get; set; } = new();
    public List<string> CustomOptions { get; set; } = new();
    public List<string> UiSelectionContent { get; set; } = new();
    public Dictionary<string, string> Hints { get; set; } = new();
    public RuntimeContents Runtimes { get; set; } = new();
    public BatchLogContents BatchLogContents { get; set; } = new();
}

[Serializable]
public sealed class RuntimeContents
{
    public string Label { get; set; } = "Runtime";

    public Dictionary<string, string> Dictionaries { get; set; } = new()
    {
        ["default"] = "Default dictionary",
        ["dicts"] = "Folder [dicts] dictionary",
        ["json"] = "JSON dictionary",
        ["cbor"] = "CBOR dictionary"
    };
}

[Serializable]
public sealed class BatchLogContents
{
    public string ConversionType { get; set; } = "";
    public string Region { get; set; } = "";
    public string ZhtwIdioms { get; set; } = "";
    public string Punctuations { get; set; } = "";
    public string ConvertFilename { get; set; } = "";
    public string OutputFolder { get; set; } = "";
}

[Serializable]
public sealed class ShortHeadingSettings
{
    public int MaxLen { get; set; } = 8;

    // JSON expects 0/1 flags
    public bool AllCjk { get; set; } = true;
    public bool AllAscii { get; set; } = true;
    public bool AllAsciiDigits { get; set; } = true;
    public bool MixedCjkAscii { get; set; } = true;

    /// <summary>
    /// Optional custom regex to treat a line as a title/heading.
    /// Leave blank to disable.
    /// </summary>
    public string? CustomTitleHeadingRegex
    {
        get => _customTitleHeadingRegex;
        set
        {
            _customTitleHeadingRegex = value ?? string.Empty;
            _customTitleHeadingRegexCompiled = null; // invalidate cache
        }
    }

    // Convenience bool views (not serialized)
    [JsonIgnore] public bool AllCjkEnabled => AllCjk;
    [JsonIgnore] public bool AllAsciiEnabled => AllAscii;
    [JsonIgnore] public bool AllAsciiDigitsEnabled => AllAsciiDigits;
    [JsonIgnore] public bool MixedCjkAsciiEnabled => MixedCjkAscii;

    /// <summary>
    /// Lazily compiled regex for <see cref="CustomTitleHeadingRegex"/>.
    /// Null when the regex string is empty or whitespace.
    /// </summary>
    [JsonIgnore]
    public Regex? CustomTitleHeadingRegexCompiled
    {
        get
        {
            var s = _customTitleHeadingRegex;
            if (string.IsNullOrWhiteSpace(s))
                return null;

            try
            {
                return _customTitleHeadingRegexCompiled ??= new Regex(
                    s,
                    RegexOptions.Compiled | RegexOptions.CultureInvariant
                );
            }
            catch (ArgumentException)
            {
                // Invalid regex -> treat as disabled
                return null;
            }
        }
    }

    /// <summary>
    /// Returns a safe MaxLen within [3, 30].
    /// Guards against invalid or user-edited JSON.
    /// </summary>
    [JsonIgnore]
    public int MaxLenClamped => Math.Clamp(MaxLen, 3, 30);

    public static ShortHeadingSettings Default => new()
    {
        MaxLen = 8,
        AllCjk = true,
        AllAscii = true,
        AllAsciiDigits = true,
        MixedCjkAscii = true,
        CustomTitleHeadingRegex = ""
    };

    private string _customTitleHeadingRegex = string.Empty;
    private Regex? _customTitleHeadingRegexCompiled;
}

[Serializable]
public sealed class SentenceBoundaryModeSetting
{
    public int Value { get; set; } = 2;
}
