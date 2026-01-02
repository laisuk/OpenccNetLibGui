using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using AvaloniaEdit.Document;
using OpenccNetLib;
using ReactiveUI;
using OpenccNetLibGui.Services;
using OpenccNetLibGui.Views;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using OpenccNetLibGui.Models;
using Avalonia.Threading;

namespace OpenccNetLibGui.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly LanguageSettings? _languageSettings;
    private readonly LanguageSettingsService? _languageSettingsService;
    private readonly Language? _selectedLanguage;
    private readonly List<string>? _textFileTypes;
    private readonly List<string>? _officeFileTypes;
    private readonly ITopLevelService? _topLevelService;
    private bool _isBtnBatchStartVisible;
    private bool _isBtnOpenFileVisible = true;
    private bool _isBtnProcessVisible = true;
    private bool _isBtnSaveFileVisible = true;
    private bool _isCmbSaveTargetVisible = true;
    private bool _isCbPunctuation = true;
    private bool _isCbZhtw;
    private bool _isCbZhtwEnabled;
    private bool _isLblFileNameVisible = true;
    private bool _isRbHk;
    private bool _isRbS2T;
    private bool _isRbStd = true;
    private bool _isRbT2S = true;
    private bool _isRbCustom;
    private bool _isRbZhtw;
    private bool _isTabBatch;
    private bool _isTabMain = true;
    private bool _isTabMessage = true;
    private bool _isTabPreview;
    private bool _isTbOutFolderFocus;
    private string? _lblDestinationCodeContent = string.Empty;
    private string? _lblFilenameContent = string.Empty;
    private string? _lblSourceCodeContent = string.Empty;
    private string? _lblStatusBarContent = string.Empty;
    private string? _lblTotalCharsContent = string.Empty;
    private ObservableCollection<string>? _lbxDestinationItems;
    private ObservableCollection<string>? _lbxSourceItems;
    private int _lbxSourceSelectedIndex;
    private string? _lbxSourceSelectedItem = string.Empty;
    private string? _rbT2SContent = "zh-Hant (繁) to zh-Hans (简)";
    private string? _rbS2TContent = "zh-Hans (简) to zh-Hant (繁)";
    private string? _rbCustomContent = "Manual (自定义)";
    private string? _rbStdContent = "General (通用简繁)";
    private string? _rbZhtwContent = "ZH-TW (中台简繁)";
    private string? _rbHkContent = "ZH-HK (中港简繁)";
    private string? _cbZhtwContent = "ZH-TW Idioms (中台惯用语)";
    private string? _cbPunctuationContent = "Punctuation (标点)";
    private FontWeight _tabBatchFontWeight = FontWeight.Normal;
    private FontWeight _tabMainFontWeight = FontWeight.Black;
    private string? _tbOutFolderText = "./output/";

    // private string? _tbPreviewText = string.Empty;
    private TextDocument? _tbPreviewTextDocument;
    private TextDocument? _tbSourceTextDocument;
    private TextDocument? _tbDestinationTextDocument;
    private string? _currentOpenFileName = string.Empty;
    private string? _selectedItem;

    private readonly Opencc? _opencc;
    private bool _isCbConvertFilename;
    private readonly int _locale;
    private PdfViewModel Pdf { get; }
    private readonly int _sentenceBoundaryLevel;

    public bool IsSettingsDirty =>
        _languageSettingsService!.IsDirty;

    public ObservableCollection<string> CustomOptions { get; } = new();

    public string? SelectedItem
    {
        get => _selectedItem;
        set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
    }

    public MainWindowViewModel()
    {
        TbSourceTextDocument = new TextDocument();
        TbDestinationTextDocument = new TextDocument();
        TbPreviewTextDocument = new TextDocument();
        LbxSourceItems = new ObservableCollection<string>();
        LbxDestinationItems = new ObservableCollection<string>();
        Pdf = new PdfViewModel();
        BtnPasteCommand = ReactiveCommand.CreateFromTask(BtnPaste);
        BtnCopyCommand = ReactiveCommand.CreateFromTask(BtnCopy);
        BtnOpenFileCommand = ReactiveCommand.CreateFromTask(BtnOpenFile);
        BtnSaveFileCommand = ReactiveCommand.CreateFromTask(BtnSaveFile);
        BtnProcessCommand = ReactiveCommand.Create(BtnProcess);
        BtnClearTbSourceCommand = ReactiveCommand.Create(BtnClearTbSource);
        BtnClearTbDestinationCommand = ReactiveCommand.Create(BtnClearTbDestination);
        BtnAddCommand = ReactiveCommand.CreateFromTask(BtnAdd);
        BtnRemoveCommand = ReactiveCommand.Create(BtnRemove);
        BtnClearLbxSourceCommand = ReactiveCommand.Create(BtnClearLbxSource);
        BtnSelectOutFolderCommand = ReactiveCommand.CreateFromTask(BtnSelectOutFolder);
        BtnPreviewCommand = ReactiveCommand.CreateFromTask(BtnPreview);
        BtnDetectCommand = ReactiveCommand.CreateFromTask(BtnDetect);
        BtnMessagePreviewClearCommand = ReactiveCommand.Create(BtnMessagePreviewClear);
        BtnBatchStartCommand = ReactiveCommand.CreateFromTask(BtnBatchStart);
        CmbCustomGotFocusCommand = ReactiveCommand.Create(() => { IsRbCustom = true; });
        BtnReflowCommand = ReactiveCommand.Create(ReflowCjkParagraphs);
        ShowShortHeadingDialogCommand = ReactiveCommand.CreateFromTask(ShowShortHeadingDialogAsync);
        SaveLanguageSettingsCommand = ReactiveCommand.Create(SaveLanguageSettings);
        ShowAboutDialog = ReactiveCommand.CreateFromTask(ShowAbout);
    }

    public MainWindowViewModel(ITopLevelService topLevelService, LanguageSettingsService languageSettingsService,
        Opencc opencc) :
        this()
    {
        _topLevelService = topLevelService;
        _languageSettingsService = languageSettingsService;
        _languageSettings = languageSettingsService.LanguageSettings;
        _locale = _languageSettings.Locale == 1 ? _languageSettings.Locale : 2;
        _selectedLanguage = _languageSettings.Languages![_locale];
        _rbT2SContent = _selectedLanguage.T2SContent ?? "zh-Hant to zh-Hans";
        _rbS2TContent = _selectedLanguage.S2TContent ?? "zh-Hans to zh-Hant";
        _rbCustomContent = _selectedLanguage.CustomContent ?? "Manual";
        _rbStdContent = _selectedLanguage.StdContent ?? "General";
        _rbZhtwContent = _selectedLanguage.ZhtwContent ?? "ZH-TW";
        _rbHkContent = _selectedLanguage.HkContent ?? "ZH-HK";
        _cbZhtwContent = _selectedLanguage.CbZhtwContent ?? "ZH-TW Idioms";
        _cbPunctuationContent = _selectedLanguage.CbPunctuationContent ?? "Punctuation";
        CustomOptions.Clear();
        if (_selectedLanguage.CustomOptions != null)
        {
            foreach (var opt in _selectedLanguage.CustomOptions!)
                CustomOptions.Add(opt);
        }

        SelectedItem =
            CustomOptions.Count > 0 ? CustomOptions[0] : "s2t (zh-Hans->zh-Hant)"; // Set "Option 1" as default
        _textFileTypes = _languageSettings.TextFileTypes ?? new List<string>();
        _officeFileTypes = _languageSettings.OfficeFileTypes ?? new List<string>();
        IsCbPunctuation = _languageSettings.Punctuation > 0;
        IsCbConvertFilename = _languageSettings.ConvertFilename > 0;

        // PDF Options (from pdfOptions)
        var po = _languageSettings.PdfOptions;

        IsAddPdfPageHeader = po.AddPdfPageHeader > 0;
        IsCompactPdfText = po.CompactPdfText > 0;
        IsAutoReflow = po.AutoReflowPdfText > 0;
        IsIgnoreUntrustedPdfText = po.IgnoreUntrustedPdfText > 0;

        ShortHeading = po.ShortHeadingSettings;
        // ShortHeadingMaxLen = ShortHeading.MaxLen; // ✅ use nested maxLen

        // Read user PdfEngine preference (1 = PdfPig, 2 = Pdfium) and verify compatibility
        var engine = PdfEngineHelper.InitPdfEngine(po.PdfEngine);
        if (engine == PdfEngine.PdfPig && po.PdfEngine == 2)
        {
            TbSourceTextDocument!.Text =
                "Pdfium not supported on this platform. Falling back to PdfPig.\n" +
                "You can set default pdfOptions.pdfEngine to 1 in LanguageSettings.json for PdfPig.";
        }

        PdfEngine = engine;

        // 1 = lenient, 2 = balanced (default), 3 = strict
        _sentenceBoundaryLevel = Math.Clamp(
            _languageSettings!.SentenceBoundaryMode!.Value,
            1,
            3
        );

        // Create PDF VM (single source of truth)
        Pdf = new PdfViewModel
        {
            PdfEngine = PdfEngine,
            IsAddPdfPageHeader = IsAddPdfPageHeader,
            IsCompactPdfText = IsCompactPdfText,
            IsAutoReflow = IsAutoReflow,
            IsIgnoreUntrustedPdfText =  IsIgnoreUntrustedPdfText,
            ShortHeading = ShortHeading,
            SentenceBoundaryLevel = _sentenceBoundaryLevel,
        };

        // Show the .NET runtime version and current dictionary in the status bar
        var runtimeVersion = RuntimeInformation.FrameworkDescription;

        switch (_languageSettings.Dictionary)
        {
            case "dicts":
                Opencc.UseCustomDictionary(DictionaryLib.FromDicts());
                LblStatusBarContent = $"Runtime: {runtimeVersion} Using folder [dicts] dictionary";
                break;
            case "json":
                Opencc.UseCustomDictionary(DictionaryLib.FromJson());
                LblStatusBarContent = $"Runtime: {runtimeVersion} Using JSON dictionary";
                break;
            case "cbor":
                Opencc.UseCustomDictionary(DictionaryLib.FromCbor());
                LblStatusBarContent = $"Runtime: {runtimeVersion} Using CBOR dictionary";
                break;
            default:
                LblStatusBarContent = $"Runtime: {runtimeVersion} Using default ZSTD dictionary";
                break;
        }

        _opencc = opencc;
    }

    #region Reactive Command Region

    public ReactiveCommand<Unit, Unit> BtnPasteCommand { get; }
    public ReactiveCommand<Unit, Unit> BtnCopyCommand { get; }
    public ReactiveCommand<Unit, Unit> BtnOpenFileCommand { get; }
    public ReactiveCommand<Unit, Unit> BtnSaveFileCommand { get; }
    public ReactiveCommand<Unit, Unit> BtnProcessCommand { get; }
    public ReactiveCommand<Unit, Unit> BtnClearTbSourceCommand { get; }
    public ReactiveCommand<Unit, Unit> BtnClearTbDestinationCommand { get; }
    public ReactiveCommand<Unit, Unit> BtnAddCommand { get; }
    public ReactiveCommand<Unit, Unit> BtnRemoveCommand { get; }
    public ReactiveCommand<Unit, Unit> BtnClearLbxSourceCommand { get; }
    public ReactiveCommand<Unit, Unit> BtnSelectOutFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> BtnPreviewCommand { get; }
    public ReactiveCommand<Unit, Unit> BtnDetectCommand { get; }
    public ReactiveCommand<Unit, Unit> BtnMessagePreviewClearCommand { get; }
    public ReactiveCommand<Unit, Unit> BtnBatchStartCommand { get; }
    public ReactiveCommand<Unit, Unit> CmbCustomGotFocusCommand { get; }
    public ReactiveCommand<Unit, Unit> BtnReflowCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowShortHeadingDialogCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveLanguageSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowAboutDialog { get; }

    #endregion

    private async Task BtnPaste()
    {
        var inputText = await _topLevelService!.GetClipboardTextAsync();

        if (string.IsNullOrEmpty(inputText))
        {
            LblStatusBarContent = "Clipboard is empty.";
            return;
        }

        TbSourceTextDocument!.UndoStack.ClearAll();
        TbSourceTextDocument!.Text = inputText;
        LblStatusBarContent = "Clipboard content pasted";
        var codeText = Opencc.ZhoCheck(inputText);
        UpdateEncodeInfo(codeText);
        LblFileNameContent = string.Empty;
        CurrentOpenFilename = string.Empty;
    }

    private async Task BtnCopy()
    {
        if (string.IsNullOrEmpty(TbDestinationTextDocument!.Text))
        {
            LblStatusBarContent = "Not copied: Destination content is empty.";
            return;
        }

        try
        {
            await _topLevelService!.SetClipboardTextAsync(TbDestinationTextDocument!.Text);
            LblStatusBarContent = "Text copied to clipboard";
        }
        catch (Exception ex)
        {
            LblStatusBarContent = $"Clipboard error: {ex.Message}";
        }
    }

    private async Task BtnOpenFile()
    {
        var mainWindow = _topLevelService!.GetMainWindow();

        var storageProvider = mainWindow.StorageProvider;
        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Text File",
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Text Files") { Patterns = new[] { "*.txt", "*.md", "*.csv", "*.html", "*.xml" } },
                new("Subtitle Files") { Patterns = new[] { "*.srt", "*.vtt", "*.ass", "*.ttml2" } },
                new("Word Documents") { Patterns = new[] { "*.docx", "*.odt" } },
                new("Pdf Files") { Patterns = new[] { "*.pdf" } },
                new("All Files") { Patterns = new[] { "*.*" } }
            },
            AllowMultiple = false
        });

        if (result.Count <= 0) return;
        var file = result[0];
        {
            var path = file.Path.LocalPath;
            var fileExt = Path.GetExtension(path);

            if (fileExt.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await UpdateTbSourcePdfAsync(path);
                }
                catch (Exception ex)
                {
                    LblStatusBarContent = $"Error opening PDF: {ex.Message}";
                }

                return;
            }

            var isTxt = _textFileTypes != null && _textFileTypes.Contains(fileExt, StringComparer.OrdinalIgnoreCase);

            if (!isTxt
                && !OpenXmlHelper.IsDocx(path)
                && !OpenXmlHelper.IsOdt(path))
            {
                LblStatusBarContent = $"Error: File type ({fileExt}) not support";
                return;
            }

            try
            {
                await UpdateTbSourceFileContentsAsync(path);
            }
            catch (Exception ex)
            {
                // Handle unexpected exceptions here
                // Console.WriteLine($"Unhandled exception: {ex}");
                LblStatusBarContent = $"Error open file: {ex.Message}";
            }
        }
    }

    #region PDF Handling Region

    // Public/simple entry point used by UI / DnD / menu etc.
    internal async Task UpdateTbSourcePdfAsync(string path)
    {
        var requestId = Pdf.NewRequestId();
        var ct = Pdf.CurrentToken;

        try
        {
            LblStatusBarContent = $"📄 Loading PDF ({Pdf.PdfEngine.ToDisplayName()})...";

            void Progress(int percent)
            {
                var msg = $"Loading PDF {BuildProgressBar(percent)}  {percent}%";
                Dispatcher.UIThread.Post(() => LblStatusBarContent = msg);
            }

            var result = await Pdf.LoadPdfAsync(path,
                Progress,
                ct);

            // stale request guard
            if (requestId != Pdf.CurrentRequestId)
                return;

            // Apply to UI (MainWindowVM responsibility)
            TbSourceTextDocument!.Text = result.Text;
            CurrentOpenFilename = path;
            var displayName = Path.GetFileName(path);
            LblFileNameContent = displayName;

            UpdateEncodeInfo(Opencc.ZhoCheck(result.Text));
            LblStatusBarContent =
                $"✅ PDF loaded ({result.PageCount:N0} pages, {result.EngineUsed.ToDisplayName()}{(result.AutoReflowApplied ? ", Auto-Reflowed" : "")}{(IsIgnoreUntrustedPdfText ? ", Ignore-Untrusted" : "")}): {displayName}";
        }
        catch (OperationCanceledException)
        {
            // optional: keep existing behavior
            LblStatusBarContent = $"⏹ PDF loading cancelled: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            LblStatusBarContent = $"❌ PDF load failed: {ex.Message}";
            // throw;
        }
    }

    private void ReflowCjkParagraphs()
    {
        var document = TbSourceTextDocument;
        var fullText = document!.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(fullText))
        {
            LblStatusBarContent = "Nothing to reflow";
            return;
        }

        var hasSelection = TbSourceSelectionLength > 0;

        string sourceText;
        var start = TbSourceSelectionStart;
        var length = TbSourceSelectionLength;

        if (hasSelection)
        {
            // Boundaries guard
            if (start < 0 || length <= 0 || start + length > fullText.Length)
            {
                // Fallback to whole document if selection is invalid
                sourceText = fullText;
                hasSelection = false;
            }
            else
            {
                sourceText = fullText.Substring(start, length);
            }
        }
        else
        {
            sourceText = fullText;
        }

        if (string.IsNullOrWhiteSpace(sourceText))
        {
            LblStatusBarContent = "Nothing to reflow";
            return;
        }

        var result =
            ReflowModel.ReflowCjkParagraphs(sourceText, IsAddPdfPageHeader, IsCompactPdfText, ShortHeading,
                _sentenceBoundaryLevel);

        // ⭐ If only reflowing a selection → ensure trailing newline
        if (hasSelection)
        {
            // Avoid double newline if already present
            if (!result.EndsWith('\n'))
                result += "\n";
        }

        if (hasSelection)
        {
            // Replace only the selected region
            var before = fullText[..start];
            var after = fullText[(start + length)..];

            var newFull = before + result + after;
            document.Text = newFull;

            // Update selection to cover the new reflowed range
            TbSourceSelectionStart = start;
            TbSourceSelectionLength = result.Length;
            TbSourceCaretOffset = start + result.Length;
        }
        else
        {
            // Reflow entire document
            document.Text = result;

            // Clear selection
            TbSourceSelectionStart = 0;
            TbSourceSelectionLength = 0;
            TbSourceCaretOffset = 0;
        }

        LblStatusBarContent = "✅ Reflow complete (CJK-aware)";
    }

    #endregion

    private async Task BtnSaveFile()
    {
        var mainWindow = _topLevelService!.GetMainWindow();

        var storageProvider = mainWindow.StorageProvider;
        var result = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Text File",
            SuggestedFileName = SelectedSaveTarget == SaveTarget.Destination ? "destination.txt" : "source.txt",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("Text Files") { Patterns = new[] { "*.txt" } }
            }
        });

        string target;
        string content;

        switch (SelectedSaveTarget)
        {
            case SaveTarget.Destination:
                target = "Destination";
                content = TbDestinationTextDocument!.Text;
                break;

            case SaveTarget.Source:
                target = "Source";
                content = TbSourceTextDocument!.Text;
                break;
            default:
                target = "Destination";
                content = string.Empty;
                break;
        }

        if (result != null)
        {
            var path = result.Path.LocalPath;
            await File.WriteAllTextAsync(path, content, Encoding.UTF8);
            LblStatusBarContent = $"{target} contents saved to file: {path}";
        }
    }

    private void BtnProcess()
    {
        if (string.IsNullOrEmpty(TbSourceTextDocument!.Text))
        {
            LblStatusBarContent = "Source content is empty.";
            return;
        }

        if (string.IsNullOrEmpty(LblSourceCodeContent))
        {
            UpdateEncodeInfo(Opencc.ZhoCheck(TbSourceTextDocument.Text));
        }

        // Check if any selected text
        var document = TbSourceTextDocument;
        var fullText = document!.Text ?? string.Empty;

        var hasSelection = TbSourceSelectionLength > 0;

        string sourceText;
        var start = TbSourceSelectionStart;
        var length = TbSourceSelectionLength;

        if (hasSelection)
        {
            // Boundaries guard
            if (start < 0 || length <= 0 || start + length > fullText.Length)
            {
                // Fallback to whole document if selection is invalid
                sourceText = fullText;
                hasSelection = false;
            }
            else
            {
                sourceText = fullText.Substring(start, length);
            }
        }
        else
        {
            sourceText = fullText;
        }

        if (string.IsNullOrWhiteSpace(sourceText))
        {
            LblStatusBarContent = "Nothing to process";
            return;
        }

        var config = GetCurrentConfig();

        if (!IsRbS2T && !IsRbT2S && !IsRbCustom) return;
        _opencc!.Config = config;

        var stopwatch = Stopwatch.StartNew();
        var convertedText = _opencc.Convert(sourceText, IsCbPunctuation);
        stopwatch.Stop();

        // Set result and clear undo history to reduce memory usage
        TbDestinationTextDocument!.Text = convertedText;
        TbDestinationTextDocument.UndoStack.ClearAll();

        if (hasSelection)
        {
            // clear selection, keep caret exactly where user left it (forward/backward ok)
            TbSourceSelectionStart = TbSourceCaretOffset; // optional, can keep range consistent
            TbSourceSelectionLength = 0;
        }

        // Set destination label
        LblDestinationCodeContent = LblSourceCodeContent!.Contains("Non")
            ? LblSourceCodeContent
            : IsRbT2S
                ? _selectedLanguage!.Name![2]
                : IsRbS2T
                    ? _selectedLanguage!.Name![1]
                    : $"Manual ( {config} )";

        LblStatusBarContent = $"Process completed: {config} —> {stopwatch.ElapsedMilliseconds} ms";
    }

    private async Task BtnBatchStart()
    {
        if (!Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output")))
            Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output"));

        if (LbxSourceItems!.Count == 0)
        {
            LblStatusBarContent = "Nothing to convert.";
            return;
        }

        if (!Directory.Exists(TbOutFolderText))
        {
            await MessageBox.Show("Invalid output folder:\n " + TbOutFolderText, "Error",
                _topLevelService!.GetMainWindow());
            IsTbOutFolderFocus = true;
            return;
        }

        if (!IsRbS2T && !IsRbT2S && !IsRbCustom)
        {
            await MessageBox.Show("Please select conversion type:\n zh-Hans / zh-Hant", "Error",
                _topLevelService!.GetMainWindow());
            return;
        }

        var config = GetCurrentConfig();
        var conversion = IsRbCustom
            ? config
            : IsRbS2T
                ? RbS2TContent
                : RbT2SContent;
        var region = IsRbStd
            ? RbStdContent
            : IsRbHk
                ? RbHkContent
                : RbZhtwContent;
        var isZhTwIdioms = IsCbZhtw ? "✔️ Yes" : "✖️ No";
        var isPunctuations = IsCbPunctuation ? "✔️ Yes" : "️✖️  ️No";
        var isConvertFilename = IsCbConvertFilename ? "✔️ Yes" : "✖️ No";

        IsTabMessage = true;
        LbxDestinationItems!.Clear();
        LbxDestinationItems.Add(_locale == 1
            ? $"Conversion Type (轉換方式) => {conversion}"
            : $"Conversion Type (转换方式) => {conversion}");
        if (!IsRbCustom)
        {
            LbxDestinationItems.Add(_locale == 1 ? $"Region (區域) => {region}" : $"Region (区域) => {region}");
            LbxDestinationItems.Add(_locale == 1
                ? $"ZH/TW Idioms (中臺慣用語) => {isZhTwIdioms}"
                : $"ZH/TW Idioms (中台惯用语) => {isZhTwIdioms}");
        }

        LbxDestinationItems.Add(_locale == 1
            ? $"Punctuations (標點) => {isPunctuations}"
            : $"Punctuations (标点) => {isPunctuations}");
        LbxDestinationItems.Add(_locale == 1
            ? $"Convert filename (轉換文件名) => {isConvertFilename}"
            : $"Convert filename (转换文件名) => {isConvertFilename}");
        LbxDestinationItems.Add(_locale == 1
            ? $"Output folder: (輸出文件夾) => {TbOutFolderText}"
            : $"Output folder: (输出文件夹) => {TbOutFolderText}");

        var count = 0;
        if (_opencc!.Config != config) _opencc.Config = config; // avoid touching when same

        foreach (var sourceFilePath in LbxSourceItems)
        {
            count++;
            var fileExt = Path.GetExtension(sourceFilePath).ToLower();
            var filenameWithoutExt = Path.GetFileNameWithoutExtension(sourceFilePath);

            if (!File.Exists(sourceFilePath))
            {
                LbxDestinationItems.Add($"({count}) {sourceFilePath} -> ❌ File not found.");
                continue;
            }

            var isText = _textFileTypes != null && _textFileTypes!.Contains(fileExt, StringComparer.OrdinalIgnoreCase);
            var isOffice = _officeFileTypes!.Contains(fileExt, StringComparer.OrdinalIgnoreCase);
            // var isPdf = fileExt.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
            var isPdf = fileExt.Equals(".pdf", StringComparison.OrdinalIgnoreCase) && PdfHelper.IsPdf(sourceFilePath);

            if (!isText && !isOffice && !isPdf)
            {
                LbxDestinationItems.Add($"({count}) [❌ File skipped ({fileExt})] {sourceFilePath}");
                continue;
            }

            var suffix =
                // Set suffix based on the radio button state
                IsRbT2S
                    ? "_Hans"
                    : IsRbS2T
                        ? "_Hant"
                        : IsRbCustom
                            ? $"_{config}"
                            : "_Other";

            if (IsCbConvertFilename)
                filenameWithoutExt = _opencc!.Convert(filenameWithoutExt, IsCbPunctuation);

            var outputFilename = Path.Combine(Path.GetFullPath(TbOutFolderText),
                filenameWithoutExt + suffix + fileExt);
            var fileExtNoDot = fileExt.Length > 1 ? fileExt[1..] : "";


            if (OfficeDocModel.IsValidOfficeFormat(fileExtNoDot))
            {
                var (success, message) = await OfficeDocModel.ConvertOfficeDocAsync(
                    sourceFilePath,
                    outputFilename,
                    fileExtNoDot,
                    _opencc,
                    IsCbPunctuation,
                    true);

                LbxDestinationItems.Add(
                    success
                        ? $"({count}) {outputFilename} -> {message}"
                        : $"({count}) [File skipped] {sourceFilePath} -> {message}"
                );
            }
            else if (isPdf)
            {
                // 🔹 New PDF batch branch
                LbxDestinationItems.Add($"({count}) ⏳ Processing PDF... please wait...");

                string pdfText;
                int pageCount;

                try
                {
                    var r = await Pdf.LoadPdfAsync(
                        sourceFilePath, progressCallback: null,
                        cancellationToken: CancellationToken.None);

                    pdfText = r.Text;
                    pageCount = r.PageCount;
                }
                catch (Exception ex)
                {
                    LbxDestinationItems.Add(
                        $"({count}) [❌ PDF error] {sourceFilePath} -> {ex.Message}");
                    continue;
                }

                // Convert with OpenCC on a background thread (if not "_Other")
                var convertedText =
                    suffix != "_Other"
                        ? await Task.Run(() => _opencc.Convert(pdfText, IsCbPunctuation))
                        : pdfText;

                // Force .txt output extension, but keep converted filename + suffix
                var pdfOutputPath = Path.Combine(
                    Path.GetFullPath(TbOutFolderText),
                    filenameWithoutExt + suffix + ".txt");

                await File.WriteAllTextAsync(pdfOutputPath, convertedText);

                LbxDestinationItems.Add($"({count}) {pdfOutputPath} ({pageCount:N0} pages) -> ✅ Done");
            }
            else
            {
                string inputText;
                try
                {
                    inputText = await File.ReadAllTextAsync(sourceFilePath);
                }
                catch (Exception)
                {
                    LbxDestinationItems.Add($"({count}) {sourceFilePath} -> ❌ Conversion error.");
                    continue;
                }

                // Run the heavy conversion on a background thread
                var convertedText = await Task.Run(() => _opencc!.Convert(inputText, IsCbPunctuation));

                await File.WriteAllTextAsync(outputFilename, convertedText);

                LbxDestinationItems.Add($"({count}) {outputFilename} -> ✅ Done");
            }
        }

        LbxDestinationItems.Add($"✅ Batch conversion ({count}) Done");
        LblStatusBarContent = $"Batch conversion done. ( {config} )";
    }

    private void BtnClearTbSource()
    {
        TbSourceTextDocument!.Text = string.Empty;
        CurrentOpenFilename = string.Empty;
        LblSourceCodeContent = string.Empty;
        LblFileNameContent = string.Empty;
        LblStatusBarContent = "Source text box cleared";
        TbSourceTextDocument.UndoStack.ClearAll();
    }

    private void BtnClearTbDestination()
    {
        TbDestinationTextDocument!.Text = string.Empty;
        LblDestinationCodeContent = string.Empty;
        LblStatusBarContent = "Destination contents cleared";
        TbDestinationTextDocument.UndoStack.ClearAll();
    }

    private async Task BtnAdd()
    {
        var mainWindow = _topLevelService!.GetMainWindow();

        var storageProvider = mainWindow.StorageProvider;
        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Text File",
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Text Files") { Patterns = new[] { "*.txt", "*.md", "*.csv", "*.html", "*.xml" } },
                new("Office Files")
                    { Patterns = new[] { "*.docx", "*.xlsx", "*.pptx", "*.odt", "*.ods", "*.odp", "*.epub" } },
                new("PDF Files") { Patterns = new[] { "*.pdf" } },
                new("All Files") { Patterns = new[] { "*.*" } }
            },
            AllowMultiple = true
        });

        if (result.Count == 0)
            return;

        // Collect current items
        var items = LbxSourceItems!.ToList();

        // Add new items (avoid duplicates)
        foreach (var file in result)
        {
            var path = file.Path.LocalPath;
            if (!items.Contains(path))
                items.Add(path);
        }

        // Separate into PDF and non-PDF lists
        var pdfList = new List<string>();
        var nonPdfList = new List<string>();

        foreach (var path in items)
        {
            if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                pdfList.Add(path);
            else
                nonPdfList.Add(path);
        }

        // Sort only non-PDF files alphabetically
        nonPdfList.Sort(StringComparer.OrdinalIgnoreCase);

        // Merge: non-PDF first, PDFs last
        LbxSourceItems!.Clear();
        foreach (var item in nonPdfList)
            LbxSourceItems.Add(item);
        foreach (var item in pdfList)
            LbxSourceItems.Add(item);
    }

    private void BtnRemove()
    {
        var index = LbxSourceSelectedIndex;
        var name = LbxSourceSelectedItem;
        if (LbxSourceSelectedIndex == -1 || LbxSourceItems!.Count == 0 || string.IsNullOrEmpty(name))
        {
            LblStatusBarContent = "Nothing to remove.";
            return;
        }

        LbxSourceItems!.Remove(LbxSourceSelectedItem!);
        LblStatusBarContent = $"Item ({index + 1}) {name} removed";
    }

    private async Task BtnPreview()
    {
        if (LbxSourceSelectedIndex == -1 || string.IsNullOrWhiteSpace(LbxSourceSelectedItem))
        {
            LblStatusBarContent = "Nothing to preview.";
            return;
        }

        var filename = LbxSourceSelectedItem;
        var extension = Path.GetExtension(filename);

        if (extension.Length > 1 && !_textFileTypes!.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            IsTabMessage = true;
            LbxDestinationItems!.Add("File type [" + extension + "] ❌ Preview not supported");
            return;
        }

        try
        {
            var displayText = await File.ReadAllTextAsync(filename!);
            IsTabPreview = true;
            // TbPreviewText = displayText;
            TbPreviewTextDocument!.Text = displayText;
            LblStatusBarContent = $"File preview: {filename}";
        }
        catch (Exception)
        {
            IsTabMessage = true;
            LbxDestinationItems!.Add($"❌ File read error: {filename}");
            LblStatusBarContent = $"File read error ({filename})";
        }
    }

    private async Task BtnDetect()
    {
        if (LbxSourceItems!.Count == 0)
        {
            LblStatusBarContent = "Nothing to detect.";
            return;
        }

        IsTabMessage = true;
        LbxDestinationItems!.Clear();

        var counter = 0;

        foreach (var item in LbxSourceItems)
        {
            ++counter;

            var fileExt = Path.GetExtension(item);

            if (_textFileTypes!.Contains(fileExt, StringComparer.OrdinalIgnoreCase))
            {
                string inputText;
                try
                {
                    inputText = await File.ReadAllTextAsync(item);
                }
                catch (Exception)
                {
                    LbxDestinationItems.Add($"({counter}) " + item + " -> ❌ File read error.");
                    continue;
                }

                var textCode = _selectedLanguage!.Name![Opencc.ZhoCheck(inputText)];
                LbxDestinationItems.Add($"({counter}) [{textCode}] {item}");
            }
            else
            {
                LbxDestinationItems.Add($"({counter}) [❌ File skipped ({fileExt})] {item}");
            }
        }

        LblStatusBarContent = "Batch zho code detection done.";
    }

    private void BtnClearLbxSource()
    {
        LbxSourceItems!.Clear();
        LblStatusBarContent = "All source entries cleared.";
    }

    private void BtnMessagePreviewClear()
    {
        if (IsTabMessage)
        {
            LbxDestinationItems!.Clear();
            LblStatusBarContent = "Messages cleared.";
        }

        else if (IsTabPreview)
        {
            // TbPreviewText = string.Empty;
            TbPreviewTextDocument!.Text = string.Empty;
            TbPreviewTextDocument.UndoStack.ClearAll();
            LblStatusBarContent = "Preview cleared.";
        }
    }

    private async Task BtnSelectOutFolder()
    {
        var mainWindow = _topLevelService!.GetMainWindow();

        // Show folder picker dialog
        var result = await mainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Output Folder"
            // InitialDirectory is not supported in FolderPickerOpenOptions, can be handled differently if needed
        });

        // Process folder picker dialog results
        if (result.Count > 0)
        {
            var folderPath = result[0].Path.LocalPath;
            TbOutFolderText = folderPath;
            IsTbOutFolderFocus = false;
            IsTbOutFolderFocus = true;
            LblStatusBarContent = $"Output folder set: {folderPath}";
        }
    }

    private void UpdateEncodeInfo(int codeText)
    {
        switch (codeText)
        {
            case 1:
                LblSourceCodeContent = _selectedLanguage!.Name![codeText];
                if (!IsRbT2S) IsRbT2S = true;
                break;

            case 2:
                LblSourceCodeContent = _selectedLanguage!.Name![codeText];
                if (!IsRbS2T) IsRbS2T = true;
                break;

            default:
                LblSourceCodeContent = _selectedLanguage!.Name![0];
                break;
        }
    }

    internal async Task UpdateTbSourceFileContentsAsync(string filename)
    {
        CurrentOpenFilename = filename;

        try
        {
            string text;

            // ---- DOCX ----
            if (OpenXmlHelper.IsDocx(filename))
            {
                text = await Task.Run(() =>
                    OpenXmlHelper.ExtractDocxAllText(filename));
            }
            // ---- ODT ----
            else if (OpenXmlHelper.IsOdt(filename))
            {
                text = await Task.Run(() =>
                    OpenXmlHelper.ExtractOdtAllText(filename));
            }
            // ---- Plain text ----
            else
            {
                var fileExt = new FileInfo(filename).Extension;
                var isTxt = _textFileTypes != null &&
                            _textFileTypes.Contains(fileExt, StringComparer.OrdinalIgnoreCase);
                if (!isTxt)
                {
                    LblStatusBarContent = $"Error: Unsupported file type. ({fileExt})";
                    return;
                }

                using var reader = new StreamReader(filename, Encoding.UTF8, true);
                text = await reader.ReadToEndAsync();
            }

            // ---- Apply to TbSource ----
            TbSourceTextDocument!.Text = text;

            // ---- UI updates ----
            LblStatusBarContent = $"File: {filename}";
            UpdateEncodeInfo(Opencc.ZhoCheck(text));

            var displayName = Path.GetFileName(filename);
            LblFileNameContent = displayName.Length > 50
                ? $"{displayName[..25]}...{displayName[^15..]}"
                : displayName;
        }
        catch (Exception ex)
        {
            TbSourceTextDocument!.Text = string.Empty;
            LblSourceCodeContent = string.Empty;
            LblStatusBarContent = $"Error opening file: {ex.Message}";
            CurrentOpenFilename = string.Empty;

            Console.WriteLine($"Exception in UpdateTbSourceFileContentsAsync: {ex}");
        }
    }

    private string GetCurrentConfig()
    {
        if (IsRbCustom)
        {
            if (string.IsNullOrWhiteSpace(SelectedItem) || SelectedItem!.IndexOf(' ') <= 0) return "s2t";
            return SelectedItem![..SelectedItem!.IndexOf(' ')];
        }

        var config = IsRbS2T
            ? IsRbStd
                ? "s2t"
                : IsRbHk
                    ? "s2hk"
                    : IsCbZhtw
                        ? "s2twp"
                        : "s2tw"
            : IsRbStd
                ? "t2s"
                : IsRbHk
                    ? "hk2s"
                    : IsCbZhtw
                        ? "tw2sp"
                        : "tw2s";
        return config;
    }

    public void TbSourceTextChanged()
    {
        LblTotalCharsContent = $"[ Ch: {TbSourceTextDocument!.Text!.Length:N0} ]";
    }

    private async Task ShowShortHeadingDialogAsync()
    {
        var owner = _topLevelService?.GetMainWindow();
        if (owner is null)
            return;

        var dialog = new ShortHeadingDialog(ShortHeading!);

        var result = await dialog.ShowDialog<ShortHeadingSettings?>(owner);

        if (result is null)
            return;

        // update in-memory state
        ShortHeading = result;

        // write back to LanguageSettings
        _languageSettings!.PdfOptions.ShortHeadingSettings = result;

        this.RaisePropertyChanged(nameof(IsSettingsDirty));
    }

    private void SaveLanguageSettings()
    {
        // Ensure VM → LanguageSettings object is already updated before calling this
        // _languageSettingsService!.Save();
        _languageSettingsService!.SaveDiffOnly();

        this.RaisePropertyChanged(nameof(IsSettingsDirty));

        // optional: toast/statusbar message
        LblStatusBarContent = $"✅ Saved: {_languageSettingsService.UserSettingsPath}";
    }

    private async Task ShowAbout()
    {
        var owner = _topLevelService?.GetMainWindow();
        if (owner is null)
            return; // or log

        var dlg = new AboutDialog
        {
            DataContext = new AboutViewModel()
        };

        await dlg.ShowDialog(owner);
    }

    private static string BuildProgressBar(int percent, int width = 10)
    {
        percent = Math.Clamp(percent, 0, 100);
        var filled = (int)((long)percent * width / 100);

        var sb = new StringBuilder(width * 4 + 2);
        sb.Append('[');
        for (var i = 0; i < filled; i++) sb.Append("🟩");
        for (var i = filled; i < width; i++) sb.Append("🟨");
        sb.Append(']');
        return sb.ToString();
    }

    #region Control Binding fields Region

    public string? LblSourceCodeContent
    {
        get => _lblSourceCodeContent;
        set => this.RaiseAndSetIfChanged(ref _lblSourceCodeContent, value);
    }

    public string? LblDestinationCodeContent
    {
        get => _lblDestinationCodeContent;
        set => this.RaiseAndSetIfChanged(ref _lblDestinationCodeContent, value);
    }

    public string? LblStatusBarContent
    {
        get => _lblStatusBarContent;
        set => this.RaiseAndSetIfChanged(ref _lblStatusBarContent, value);
    }

    public string? CurrentOpenFilename
    {
        get => _currentOpenFileName;
        set => this.RaiseAndSetIfChanged(ref _currentOpenFileName, value);
    }

    public string? LblFileNameContent
    {
        get => _lblFilenameContent;
        set => this.RaiseAndSetIfChanged(ref _lblFilenameContent, value);
    }

    public TextDocument? TbSourceTextDocument
    {
        get => _tbSourceTextDocument;
        set => this.RaiseAndSetIfChanged(ref _tbSourceTextDocument, value);
    }

    public string? LblTotalCharsContent
    {
        get => _lblTotalCharsContent;
        set => this.RaiseAndSetIfChanged(ref _lblTotalCharsContent, value);
    }

    private int _tbSourceSelectionStart;

    public int TbSourceSelectionStart
    {
        get => _tbSourceSelectionStart;
        set => this.RaiseAndSetIfChanged(ref _tbSourceSelectionStart, value);
    }

    private int _tbSourceSelectionLength;

    public int TbSourceSelectionLength
    {
        get => _tbSourceSelectionLength;
        set => this.RaiseAndSetIfChanged(ref _tbSourceSelectionLength, value);
    }

    private int _tbSourceCaretOffset;

    public int TbSourceCaretOffset
    {
        get => _tbSourceCaretOffset;
        set => this.RaiseAndSetIfChanged(ref _tbSourceCaretOffset, value);
    }

    public TextDocument? TbDestinationTextDocument
    {
        get => _tbDestinationTextDocument;
        set => this.RaiseAndSetIfChanged(ref _tbDestinationTextDocument, value);
    }

    public TextDocument? TbPreviewTextDocument
    {
        get => _tbPreviewTextDocument;
        set => this.RaiseAndSetIfChanged(ref _tbPreviewTextDocument, value);
    }

    public ObservableCollection<string>? LbxSourceItems
    {
        get => _lbxSourceItems;
        set => this.RaiseAndSetIfChanged(ref _lbxSourceItems, value);
    }

    public ObservableCollection<string>? LbxDestinationItems
    {
        get => _lbxDestinationItems;
        set => this.RaiseAndSetIfChanged(ref _lbxDestinationItems, value);
    }

    public int LbxSourceSelectedIndex
    {
        get => _lbxSourceSelectedIndex;
        set => this.RaiseAndSetIfChanged(ref _lbxSourceSelectedIndex, value);
    }

    public string? LbxSourceSelectedItem
    {
        get => _lbxSourceSelectedItem;
        set => this.RaiseAndSetIfChanged(ref _lbxSourceSelectedItem, value);
    }

    public string? TbOutFolderText
    {
        get => _tbOutFolderText;
        set => this.RaiseAndSetIfChanged(ref _tbOutFolderText, value);
    }

    // public string? TbPreviewText
    // {
    //     get => _tbPreviewText;
    //     set => this.RaiseAndSetIfChanged(ref _tbPreviewText, value);
    // }

    public string? RbS2TContent
    {
        get => _rbS2TContent;
        set => this.RaiseAndSetIfChanged(ref _rbS2TContent, value);
    }

    public string? RbT2SContent
    {
        get => _rbT2SContent;
        set => this.RaiseAndSetIfChanged(ref _rbT2SContent, value);
    }

    public string? RbCustomContent
    {
        get => _rbCustomContent;
        set => this.RaiseAndSetIfChanged(ref _rbCustomContent, value);
    }

    public string? RbStdContent
    {
        get => _rbStdContent;
        set => this.RaiseAndSetIfChanged(ref _rbStdContent, value);
    }

    public string? RbZhtwContent
    {
        get => _rbZhtwContent;
        set => this.RaiseAndSetIfChanged(ref _rbZhtwContent, value);
    }

    public string? RbHkContent
    {
        get => _rbHkContent;
        set => this.RaiseAndSetIfChanged(ref _rbHkContent, value);
    }

    public string? CbZhtwContent
    {
        get => _cbZhtwContent;
        set => this.RaiseAndSetIfChanged(ref _cbZhtwContent, value);
    }

    public string? CbPunctuationContent
    {
        get => _cbPunctuationContent;
        set => this.RaiseAndSetIfChanged(ref _cbPunctuationContent, value);
    }

    #endregion

    #region Save Target Region

    public enum SaveTarget
    {
        Destination,
        Source
    }

    public IReadOnlyList<SaveTarget> SaveTargets { get; } =
        Enum.GetValues<SaveTarget>().ToList();

    private SaveTarget _selectedSaveTarget = SaveTarget.Destination;

    public SaveTarget SelectedSaveTarget
    {
        get => _selectedSaveTarget;
        set => this.RaiseAndSetIfChanged(ref _selectedSaveTarget, value);
    }

    #endregion

    #region RbCb Boolean Binding Region

    public bool IsRbS2T
    {
        get => _isRbS2T;
        set
        {
            this.RaiseAndSetIfChanged(ref _isRbS2T, value);
            if (!value) return;
            IsRbT2S = false;
            // IsRbSegment = false;
            // IsRbTag = false;
            LblSourceCodeContent = _selectedLanguage!.Name![2];
        }
    }

    public bool IsRbT2S
    {
        get => _isRbT2S;
        set
        {
            this.RaiseAndSetIfChanged(ref _isRbT2S, value);
            if (!value) return;
            IsRbS2T = false;
            LblSourceCodeContent = _selectedLanguage!.Name![1];
        }
    }

    public bool IsRbCustom
    {
        get => _isRbCustom;
        set
        {
            this.RaiseAndSetIfChanged(ref _isRbCustom, value);
            if (!value) return;
            IsRbS2T = false;
            IsRbT2S = false;
        }
    }

    public bool IsRbStd
    {
        get => _isRbStd;
        set
        {
            this.RaiseAndSetIfChanged(ref _isRbStd, value);
            if (!value) return;
            IsRbZhtw = false;
            IsRbHk = false;
            IsCbZhtw = false;
            IsCbZhtwEnabled = false;
        }
    }

    public bool IsRbZhtw
    {
        get => _isRbZhtw;
        set
        {
            this.RaiseAndSetIfChanged(ref _isRbZhtw, value);
            if (!value) return;
            IsRbStd = false;
            IsRbHk = false;
            IsCbZhtw = true;
            IsCbZhtwEnabled = true;
        }
    }

    public bool IsRbHk
    {
        get => _isRbHk;
        set
        {
            this.RaiseAndSetIfChanged(ref _isRbHk, value);
            if (!value) return;
            IsRbStd = false;
            IsRbZhtw = false;
            IsCbZhtw = false;
            IsCbZhtwEnabled = false;
        }
    }

    public bool IsTabMain
    {
        get => _isTabMain;
        set
        {
            this.RaiseAndSetIfChanged(ref _isTabMain, value);
            if (!value) return;
            IsTabBatch = false;
            IsBtnOpenFileVisible = true;
            IsLblFileNameVisible = true;
            IsCmbSaveTargetVisible = true;
            IsBtnSaveFileVisible = true;
            IsBtnProcessVisible = true;
            IsBtnBatchStartVisible = false;
            TabMainFontWeight = FontWeight.Black;
            TabBatchFontWeight = FontWeight.Normal;
        }
    }

    public bool IsTabBatch
    {
        get => _isTabBatch;
        set
        {
            this.RaiseAndSetIfChanged(ref _isTabBatch, value);
            if (!value) return;
            IsTabMain = false;
            IsBtnOpenFileVisible = false;
            IsLblFileNameVisible = false;
            IsCmbSaveTargetVisible = false;
            IsBtnSaveFileVisible = false;
            IsBtnProcessVisible = false;
            IsBtnBatchStartVisible = true;
            TabMainFontWeight = FontWeight.Normal;
            TabBatchFontWeight = FontWeight.Black;
        }
    }

    public bool IsTabMessage
    {
        get => _isTabMessage;
        set
        {
            this.RaiseAndSetIfChanged(ref _isTabMessage, value);
            if (!value) return;
            IsTabPreview = false;
        }
    }

    public bool IsTabPreview
    {
        get => _isTabPreview;
        set
        {
            this.RaiseAndSetIfChanged(ref _isTabPreview, value);
            if (!value) return;
            IsTabMessage = false;
        }
    }

    public bool IsCbZhtw
    {
        get => _isCbZhtw;
        set
        {
            this.RaiseAndSetIfChanged(ref _isCbZhtw, value);
            if (!value) return;
            IsRbHk = false;
            IsRbStd = false;
        }
    }

    public bool IsCbZhtwEnabled
    {
        get => _isCbZhtwEnabled;
        set => this.RaiseAndSetIfChanged(ref _isCbZhtwEnabled, value);
    }

    public bool IsTbOutFolderFocus
    {
        get => _isTbOutFolderFocus;
        set => this.RaiseAndSetIfChanged(ref _isTbOutFolderFocus, value);
    }

    public bool IsBtnOpenFileVisible
    {
        get => _isBtnOpenFileVisible;
        set => this.RaiseAndSetIfChanged(ref _isBtnOpenFileVisible, value);
    }

    public bool IsCmbSaveTargetVisible
    {
        get => _isCmbSaveTargetVisible;
        set => this.RaiseAndSetIfChanged(ref _isCmbSaveTargetVisible, value);
    }

    public bool IsBtnSaveFileVisible
    {
        get => _isBtnSaveFileVisible;
        set => this.RaiseAndSetIfChanged(ref _isBtnSaveFileVisible, value);
    }

    public bool IsBtnProcessVisible
    {
        get => _isBtnProcessVisible;
        set => this.RaiseAndSetIfChanged(ref _isBtnProcessVisible, value);
    }

    public bool IsLblFileNameVisible
    {
        get => _isLblFileNameVisible;
        set => this.RaiseAndSetIfChanged(ref _isLblFileNameVisible, value);
    }

    public bool IsBtnBatchStartVisible
    {
        get => _isBtnBatchStartVisible;
        set => this.RaiseAndSetIfChanged(ref _isBtnBatchStartVisible, value);
    }

    public FontWeight TabMainFontWeight
    {
        get => _tabMainFontWeight;
        set => this.RaiseAndSetIfChanged(ref _tabMainFontWeight, value);
    }

    public FontWeight TabBatchFontWeight
    {
        get => _tabBatchFontWeight;
        set => this.RaiseAndSetIfChanged(ref _tabBatchFontWeight, value);
    }

    #endregion

    #region User Customizable Properties Region

    public bool IsCbPunctuation
    {
        get => _isCbPunctuation;
        set
        {
            if (IsCbPunctuation == value) return;
            this.RaiseAndSetIfChanged(ref _isCbPunctuation, value);
            _languageSettings!.Punctuation = value ? 1 : 0;
            this.RaisePropertyChanged(nameof(IsSettingsDirty));
        }
    }

    public bool IsCbConvertFilename
    {
        get => _isCbConvertFilename;
        set
        {
            if (IsCbConvertFilename == value) return;
            _languageSettings!.ConvertFilename = value ? 1 : 0;
            this.RaiseAndSetIfChanged(ref _isCbConvertFilename, value);
            this.RaisePropertyChanged(nameof(IsSettingsDirty));
        }
    }

    public bool IsAddPdfPageHeader
    {
        get => Pdf.IsAddPdfPageHeader;
        set
        {
            if (Pdf.IsAddPdfPageHeader == value)
                return;

            Pdf.IsAddPdfPageHeader = value;
            _languageSettings!.PdfOptions.AddPdfPageHeader = value ? 1 : 0;

            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(IsSettingsDirty));
        }
    }

    public bool IsCompactPdfText
    {
        get => Pdf.IsCompactPdfText;
        set
        {
            if (Pdf.IsCompactPdfText == value)
                return;

            Pdf.IsCompactPdfText = value;
            _languageSettings!.PdfOptions.CompactPdfText = value ? 1 : 0;

            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(IsSettingsDirty));
        }
    }

    public bool IsAutoReflow
    {
        get => Pdf.IsAutoReflow;
        set
        {
            if (Pdf.IsAutoReflow == value)
                return;

            Pdf.IsAutoReflow = value;
            _languageSettings!.PdfOptions.AutoReflowPdfText = value ? 1 : 0;

            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(IsSettingsDirty));
        }
    }
    
    public bool CanIgnoreUntrustedPdfText => IsPdfiumEngine;
    
    public bool IsIgnoreUntrustedPdfText
    {
        get => Pdf.IsIgnoreUntrustedPdfText;
        set
        {
            // ✅ hard gate: cannot enable under PdfPig
            if (value && IsPdfPigEngine)
                return;
            
            if (Pdf.IsIgnoreUntrustedPdfText == value)
                return;

            Pdf.IsIgnoreUntrustedPdfText = value;
            _languageSettings!.PdfOptions.IgnoreUntrustedPdfText = value ? 1 : 0;

            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(IsSettingsDirty));
        }
    }

    public PdfEngine PdfEngine
    {
        get => Pdf.PdfEngine;
        set
        {
            if (Pdf.PdfEngine == value)
                return;

            Pdf.PdfEngine = value;
            // Sync settings object (no magic numbers)
            _languageSettings!.PdfOptions.PdfEngine = (int)value;
            
            // ✅ PDFium-only option: force off when switching to PdfPig
            if (Pdf.PdfEngine == PdfEngine.PdfPig && Pdf.IsIgnoreUntrustedPdfText)
            {
                // go through wrapper so it syncs settings + raises dirty
                IsIgnoreUntrustedPdfText = false;
            }

            // PdfEngine changed → notify RadioButtons
            this.RaisePropertyChanged(); // ✅ add this for self-changed
            this.RaisePropertyChanged(nameof(IsPdfPigEngine));
            this.RaisePropertyChanged(nameof(IsPdfiumEngine));
            // ✅ dependent enable state
            this.RaisePropertyChanged(nameof(CanIgnoreUntrustedPdfText));
            this.RaisePropertyChanged(nameof(IsSettingsDirty));
        }
    }

    // ⚙️ Use PdfPig engine
    public bool IsPdfPigEngine
    {
        get => Pdf.PdfEngine == PdfEngine.PdfPig;
        set
        {
            if (!value || Pdf.PdfEngine == PdfEngine.PdfPig) return;
            PdfEngine = PdfEngine.PdfPig; // ✅ go through wrapper
        }
    }

    // ⚙️ Use Pdfium engine
    public bool IsPdfiumEngine
    {
        get => Pdf.PdfEngine == PdfEngine.Pdfium;
        set
        {
            if (!value || Pdf.PdfEngine == PdfEngine.Pdfium) return;
            PdfEngine = PdfEngine.Pdfium; // ✅ go through wrapper
        }
    }

    public ShortHeadingSettings? ShortHeading
    {
        get => Pdf.ShortHeading;
        set
        {
            if (Equals(Pdf.ShortHeading, value)) return; // ✅ optional, avoids spam
            Pdf.ShortHeading = value;

            if (_languageSettings is not null)
            {
                _languageSettings.PdfOptions.ShortHeadingSettings =
                    value ?? ShortHeadingSettings.Default;
            }

            this.RaisePropertyChanged(); // ✅
        }
    }

    #endregion
}