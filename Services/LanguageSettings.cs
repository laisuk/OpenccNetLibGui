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
    public Dictionary<string, string>? PunctuationChars { get; set; }
    public List<string>? TextFileTypes { get; set; }
    public List<string>? OfficeFileTypes { get; set; }
    public string? Dictionary { get; set; }
    public int Locale { get; set; }
    public int UiScale { get; set; } = 100;
    public int WindowWidth { get; set; } = 1000;
    public int WindowHeight { get; set; } = 750;
    public string ThemeMode { get; set; } = "System";
    public string EditorFont { get; set; } = "Consolas";
    public double EditorFontSize { get; set; } = 14;
    public bool Punctuation { get; set; }
    public bool ConvertFilename { get; set; }
    public bool ExtendUnicodeCompat { get; set; }
    public string DeTofuLevel { get; set; } = "B";

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
    public string UiScaleContent { get; set; } = "UI Scale";
    public string ResetWindowSizeContent { get; set; } = "Reset Window Size";
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
    public string ExtendUnicodeCompatContent { get; set; } = "Extend Unicode Compatibility for CJK text normalization";
    public string DeTofuLevelContent { get; set; } = "DeTofu level";
    public string EditorFontContent { get; set; } = "Editor Font";
    public string EditorFontSizeContent { get; set; } = "Font Size";
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
    public string GlobalDictionaryContent { get; set; } = "Global Conversion Dictionary";
    public string AboutContent { get; set; } = "About...";
    public string ThemeModeContent { get; set; } = "Theme Mode";
    public List<string> ThemeModeSelectionContent { get; set; } = new();
    public List<string> SaveTargetSelectionContent { get; set; } = new();
    public List<string> CustomOptions { get; set; } = new();
    public List<string> UiSelectionContent { get; set; } = new();
    public Dictionary<string, string> Hints { get; set; } = new();
    public RuntimeContents Runtimes { get; set; } = new();
    public BatchLogContents BatchLogContents { get; set; } = new();
    public DictionaryGeneratorContents DictionaryGeneratorContents { get; set; } = new();
}

[Serializable]
public sealed class DictionaryGeneratorContents
{
    public string TabTitle { get; set; } = "Dictionary";
    public string PageTitle { get; set; } = "Dictionary Generation";
    public string BaseDirectoryLabel { get; set; } = "Base Dictionary Directory";
    public string OutputDirectoryLabel { get; set; } = "Output Directory";
    public string CustomSlotsLabel { get; set; } = "Custom Dictionary Slots";
    public string SlotColumn { get; set; } = "Slot";
    public string ModeColumn { get; set; } = "Mode";
    public string DictionaryFileColumn { get; set; } = "Dictionary file";
    public string DictionaryFilePlaceholder { get; set; } = "Dictionary file path";
    public string BrowseButton { get; set; } = "Browse";
    public string RemoveButton { get; set; } = "Remove";
    public string AddCustomDictionaryButton { get; set; } = "Add Custom Dictionary";
    public string GenerateZstdButton { get; set; } = "Generate ZSTD";
    public string GenerateCborButton { get; set; } = "Generate CBOR";
    public string GenerateJsonButton { get; set; } = "Generate JSON";
    public string ReadableUnicodeJsonLabel { get; set; } = "Readable Unicode JSON";
    public string BaseDirectoryToolTip { get; set; } = "Directory containing the standard OpenccNet text dictionaries.";

    public string OutputDirectoryToolTip { get; set; } =
        "Existing directory where the generated dictionary will be saved.";

    public string BrowseBaseDirectoryToolTip { get; set; } = "Select the base dictionary directory.";
    public string BrowseOutputDirectoryToolTip { get; set; } = "Select the output directory.";
    public string SlotToolTip { get; set; } = "Select the dictionary slot to customize.";
    public string ModeToolTip { get; set; } = "Append merges entries; Override replaces the selected slot.";
    public string DictionaryFileToolTip { get; set; } = "Path to a custom dictionary text file.";
    public string BrowseDictionaryFileToolTip { get; set; } = "Select a custom dictionary text file.";
    public string RemoveRowToolTip { get; set; } = "Remove this custom dictionary row.";
    public string AddRowToolTip { get; set; } = "Add a custom dictionary row at the end.";
    public string GenerateZstdToolTip { get; set; } = "Generate dictionary_maxlength.zstd.";
    public string GenerateCborToolTip { get; set; } = "Generate dictionary_maxlength.cbor.";
    public string GenerateJsonToolTip { get; set; } = "Generate dictionary_maxlength.json.";

    public string ReadableUnicodeJsonToolTip { get; set; } =
        "Write readable Unicode characters instead of escaped JSON sequences.";

    public string BaseDirectoryPickerTitle { get; set; } = "Select Base Dictionary Directory";
    public string OutputDirectoryPickerTitle { get; set; } = "Select Output Directory";
    public string CustomFilePickerTitle { get; set; } = "Select Custom Dictionary";
    public string DictionaryTextFilesFilter { get; set; } = "Dictionary text files";
    public string AllFilesFilter { get; set; } = "All files";
    public string GeneratingStatus { get; set; } = "Generating dictionary…";
    public string GenerationSuccessFormat { get; set; } = "Dictionary generated successfully:{0}{1}";
    public string GenerationFailedFormat { get; set; } = "Dictionary generation failed: {0}";
    public string BaseDirectoryRequired { get; set; } = "Base dictionary directory is required.";
    public string BaseDirectoryNotFoundFormat { get; set; } = "Base dictionary directory not found: {0}";
    public string OutputDirectoryRequired { get; set; } = "Output directory is required.";
    public string OutputDirectoryNotFoundFormat { get; set; } = "Output directory not found: {0}";
    public string UnsupportedSlotFormat { get; set; } = "Custom dictionary row {0}: unsupported dictionary slot: {1}.";
    public string UnsupportedModeFormat { get; set; } = "Custom dictionary row {0}: unsupported dictionary mode: {1}.";
    public string RowFileRequiredFormat { get; set; } = "Custom dictionary row {0}: file path is required.";
    public string RowFileNotFoundFormat { get; set; } = "Custom dictionary row {0}: file not found: {1}";
    public string MessageBoxTitle { get; set; } = "Dictionary Generation";
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

    public Dictionary<string, string> Statuses { get; set; } = new()
    {
        ["statusBtnPasteEmpty"] = "Clipboard is empty",
        ["statusBtnPastePasted"] = "Clipboard content pasted",
        ["statusBtnCopyDestinationEmpty"] = "Not copied: Destination content is empty.",
        ["statusBtnCopyCopied"] = "Text copied to clipboard",
        ["statusBtnCopyClipboardError"] = "Clipboard error: {0}",
        ["statusOpenFileError"] = "Error opening file: {0}",
        ["statusOpenFileLoaded"] = "File: {0}",
        ["statusPdfLoading"] = "Loading PDF ({0})...",
        ["statusPdfLoadingProgress"] = "Loading PDF {0}  {1}%",
        ["statusPdfLoaded"] = "PDF loaded ({0:N0} pages, {1}{2}{3}): {4}",
        ["statusPdfCancelled"] = "PDF loading cancelled: {0}",
        ["statusPdfLoadFailed"] = "PDF load failed: {0}",
        ["statusReflowEmpty"] = "Nothing to reflow",
        ["statusReflowComplete"] = "Reflow complete (CJK-aware)",
        ["statusNormalizeCompatEmpty"] = "Nothing to normalize",
        ["statusNormalizeCompatComplete"] = "Compatibility ideograph normalization complete",
        ["statusDialogQuoteNormalizeEmpty"] = "No dialog quote text to normalize",
        ["statusDialogQuoteDestinationNotReady"] = "Destination editor is not ready",
        ["statusDialogQuoteNormalizeComplete"] = "Dialog quote normalization complete",
        ["statusDialogQuoteValidationPassed"] = "Dialog quote validation passed",
        ["statusDialogQuoteValidationSuspiciousLines"] = "Found {0} suspicious dialog quote line(s)",
        ["dialogQuoteValidationTitle"] = "Dialog Quote Validation",
        ["dialogQuoteValidationWarningTitle"] = "Validation Warning",
        ["dialogQuoteValidationOkButton"] = "OK",
        ["dialogQuoteValidationCloseButton"] = "Close",
        ["dialogQuoteValidationNoIssues"] = "No suspicious dialog quote issues found.",
        ["dialogQuoteValidationFoundLines"] = "Found {0} suspicious dialog quote line(s).",
        ["dialogQuoteValidationHintTitle"] = "Hint:",
        ["dialogQuoteValidationHintMissingExtra"] =
            "The actual typo is often a missing, extra, reversed, or mixed dialog quote.",
        ["dialogQuoteValidationHintAbove"] = "It may appear on the reported line or a few lines above it.",
        ["dialogQuoteValidationHintFixAgain"] = "Fix the source text and validate again.",
        ["dialogQuoteValidationMoreLines"] = "...and {0} more.",
        ["statusDeTofuEmpty"] = "Nothing to DeTofu",
        ["statusDeTofuComplete"] = "DeTofu complete",
        ["statusSaveFileSaved"] = "{0} contents saved to file: {1}",
        ["statusProcessSourceEmpty"] = "Source content is empty.",
        ["statusProcessNothing"] = "Nothing to process",
        ["statusProcessCompleted"] = "Process completed: {0} -> {1} ms",
        ["statusBatchNothingToConvert"] = "Nothing to convert.",
        ["statusBatchDone"] = "Batch conversion done. ( {0} )",
        ["statusClearSource"] = "Source text box cleared",
        ["statusClearDestination"] = "Destination contents cleared",
        ["statusRemoveNothing"] = "Nothing to remove.",
        ["statusRemoveItem"] = "Item ({0}) {1} removed",
        ["statusPreviewNothing"] = "Nothing to preview.",
        ["statusPreviewFile"] = "File preview: {0}",
        ["statusPreviewReadError"] = "File read error ({0})",
        ["statusDetectNothing"] = "Nothing to detect.",
        ["statusDetectDone"] = "Batch zho code detection done.",
        ["statusClearSourceList"] = "All source entries cleared.",
        ["statusMessagesCleared"] = "Messages cleared.",
        ["statusPreviewCleared"] = "Preview cleared.",
        ["statusOutputFolderSet"] = "Output folder set: {0}",
        ["statusSettingsSaved"] = "Saved: {0}",
        ["statusPdfAutoReflowed"] = ", Auto-Reflowed",
        ["statusPdfIgnoreUntrusted"] = ", Ignore-Untrusted"
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