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
using OpenccNetLibGui.Models;

namespace OpenccNetLibGui.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    // private readonly List<Language>? _languagesInfo;
    private readonly Language? _selectedLanguage;
    private readonly List<string>? _textFileTypes;
    private readonly List<string>? _officeFileTypes;
    private readonly ITopLevelService? _topLevelService;
    private bool _isBtnBatchStartVisible;
    private bool _isBtnOpenFileVisible = true;
    private bool _isBtnProcessVisible = true;
    private bool _isBtnSaveFileVisible = true;
    private bool _isCbPunctuation = true;
    private bool _isCbZhtw;
    private bool _isCbZhtwEnabled;
    private bool _isCbConvertFilename;
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
    private readonly int _locale;

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
    }

    public MainWindowViewModel(ITopLevelService topLevelService, LanguageSettingsService languageSettingsService,
        Opencc opencc) :
        this()
    {
        _topLevelService = topLevelService;
        var languageSettings = languageSettingsService.LanguageSettings!;
        // _languagesInfo = languageSettings?.Languages;
        _locale = languageSettings.Locale == 1 ? languageSettings.Locale : 2;
        _selectedLanguage = languageSettings.Languages![_locale];
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

        SelectedItem = CustomOptions[0]; // Set "Option 1" as default
        _textFileTypes = languageSettings.TextFileTypes;
        _officeFileTypes = languageSettings.OfficeFileTypes;

        switch (languageSettings.Dictionary)
        {
            case "dicts":
                Opencc.UseCustomDictionary(DictionaryLib.FromDicts());
                LblStatusBarContent = "Using folder [dicts] dictionary";
                break;
            case "json":
                Opencc.UseCustomDictionary(DictionaryLib.FromJson());
                LblStatusBarContent = "Using JSON dictionary";
                break;
            case "cbor":
                Opencc.UseCustomDictionary(DictionaryLib.FromCbor());
                LblStatusBarContent = "Using CBOR dictionary";
                break;
            default:
                LblStatusBarContent = "Using default ZSTD dictionary";
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
        _currentOpenFileName = string.Empty;
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
                new("Text Files") { Patterns = new[] { "*.txt" } },
                new("All Files") { Patterns = new[] { "*.*" } }
            },
            AllowMultiple = false
        });

        if (result.Count <= 0) return;
        var file = result[0];
        {
            var path = file.Path.LocalPath;
            await UpdateTbSourceFileContents(path);
        }
    }

    private async Task BtnSaveFile()
    {
        var mainWindow = _topLevelService!.GetMainWindow();

        var storageProvider = mainWindow.StorageProvider;
        var result = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Text File",
            SuggestedFileName = "document.txt",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("Text Files") { Patterns = new[] { "*.txt" } },
                new("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (result != null)
        {
            var path = result.Path.LocalPath;
            await File.WriteAllTextAsync(path, TbDestinationTextDocument!.Text);
            LblStatusBarContent = $"Destination contents saved to file: {path}";
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

        var config = GetCurrentConfig();

        // Preload text before timing to exclude Avalonia's first-access cost
        var inputText = TbSourceTextDocument.Text;

        if (!IsRbS2T && !IsRbT2S && !IsRbCustom) return;
        _opencc!.Config = config;

        var stopwatch = Stopwatch.StartNew();
        var convertedText = _opencc.Convert(inputText, IsCbPunctuation);
        stopwatch.Stop();

        // Set result and clear undo history to reduce memory usage
        TbDestinationTextDocument!.Text = convertedText;
        TbDestinationTextDocument.UndoStack.ClearAll();

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
        var outputRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output");
        Directory.CreateDirectory(outputRoot);

        if (LbxSourceItems?.Count == 0)
        {
            LblStatusBarContent = "Nothing to convert.";
            return;
        }

        if (!Directory.Exists(TbOutFolderText))
        {
            await MessageBox.Show($"Invalid output folder:\n {TbOutFolderText}", "Error",
                _topLevelService!.GetMainWindow());
            IsTbOutFolderFocus = true;
            return;
        }

        if (!(IsRbS2T || IsRbT2S || IsRbCustom))
        {
            await MessageBox.Show("Please select conversion type:\n zh-Hans / zh-Hant", "Error",
                _topLevelService!.GetMainWindow());
            return;
        }

        // Configs
        var config = GetCurrentConfig();
        var conversion = IsRbCustom ? $"{RbCustomContent} -> {config}" :
            IsRbS2T ? RbS2TContent : RbT2SContent;
        var region = IsRbCustom ? RbCustomContent :
            IsRbStd ? RbStdContent :
            IsRbHk ? RbHkContent : RbZhtwContent;
        var isZhTwIdioms = IsRbCustom ? RbCustomContent : IsCbZhtw ? "✔️ Yes" : "✖️ No";
        var isPunctuations = IsCbPunctuation ? "✔️ Yes" : "✖️ No";
        var isConvertFilename = IsCbConvertFilename ? "✔️ Yes" : "✖️ No";

        // UI output setup
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

        var suffix = IsRbT2S ? "_Hans" :
            IsRbS2T ? "_Hant" :
            IsRbCustom ? $"_{config}" : "_Other";

        foreach (var sourceFilePath in LbxSourceItems!)
        {
            count++;
            var fileExt = Path.GetExtension(sourceFilePath).ToLowerInvariant();
            var fileExtNoDot = fileExt.Length > 1 ? fileExt[1..] : "";
            var filenameWithoutExt = Path.GetFileNameWithoutExtension(sourceFilePath);

            if (!File.Exists(sourceFilePath))
            {
                LbxDestinationItems.Add($"({count}) {sourceFilePath} -> ❌ File not found.");
                continue;
            }

            if (fileExt.Length != 0 && !(_textFileTypes!.Contains(fileExt) || _officeFileTypes!.Contains(fileExt)))
            {
                LbxDestinationItems.Add($"({count}) [❌ File skipped ({fileExt})] {sourceFilePath}");
                continue;
            }

            filenameWithoutExt = IsCbConvertFilename
                ? _opencc.Convert(filenameWithoutExt, IsCbPunctuation)
                : filenameWithoutExt;

            var outputFilename = Path.Combine(TbOutFolderText, filenameWithoutExt + suffix + fileExt);

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
            else
            {
                try
                {
                    var inputText = await File.ReadAllTextAsync(sourceFilePath).ConfigureAwait(false);
                    var convertedText = suffix != "_Other" ? _opencc.Convert(inputText, IsCbPunctuation) : inputText;
                    await File.WriteAllTextAsync(outputFilename, convertedText).ConfigureAwait(false);
                    LbxDestinationItems.Add($"({count}) {outputFilename} -> ✅ Done");
                }
                catch (Exception ex)
                {
                    LbxDestinationItems.Add($"({count}) {sourceFilePath} -> ❌ Error: {ex.Message}");
                }
            }
        }

        LbxDestinationItems.Add($"✅ Batch conversion ({count}) Done");
        LblStatusBarContent = $"Batch conversion done ({config})";
    }

    private void BtnClearTbSource()
    {
        TbSourceTextDocument!.Text = string.Empty;
        _currentOpenFileName = string.Empty;
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
            Title = "Open Text/Office File(s)",
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Text Files") { Patterns = new[] { "*.txt" } },
                new("Office Files")
                    { Patterns = new[] { "*.docx", "*.xlsx", "*.pptx", "*.odt", "*.ods", "*.odp", "*.epub" } },
                new("All Files") { Patterns = new[] { "*.*" } }
            },
            AllowMultiple = true
        });

        if (result.Count <= 0) return;
        var listBoxItems = LbxSourceItems!.ToList();
        var counter = 0;
        foreach (var file in result)
        {
            var path = file.Path.LocalPath;
            if (listBoxItems.Contains(path)) continue;
            listBoxItems.Add(path);
            counter++;
        }

        var sortedList = listBoxItems.OrderBy(x => x);
        LbxSourceItems!.Clear();
        foreach (var item in sortedList) LbxSourceItems.Add(item);
        LblStatusBarContent = $"File(s) added: {counter}";
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

        if (extension.Length > 1 && !_textFileTypes!.Contains(extension))
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

        foreach (var item in LbxSourceItems)
        {
            var fileExt = Path.GetExtension(item);

            if (fileExt.Length == 0 || _textFileTypes!.Contains(fileExt))
            {
                string inputText;
                try
                {
                    inputText = await File.ReadAllTextAsync(item);
                }
                catch (Exception)
                {
                    LbxDestinationItems.Add(item + " -> ❌ File read error.");
                    continue;
                }

                var textCode = _selectedLanguage!.Name![Opencc.ZhoCheck(inputText)];
                LbxDestinationItems.Add($"[{textCode}] {item}");
            }
            else
            {
                LbxDestinationItems.Add($"[❌ File skipped ({fileExt})] {item}");
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
            TbPreviewTextDocument._undoStack.ClearAll();
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

    internal void UpdateEncodeInfo(int codeText)
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

    private async Task UpdateTbSourceFileContents(string filename)
    {
        var fileInfo = new FileInfo(filename);
        if (fileInfo.Length > int.MaxValue)
        {
            LblStatusBarContent = "Error: File too large";
            return;
        }

        _currentOpenFileName = filename;

        // Read file contents
        try
        {
            using var reader = new StreamReader(_currentOpenFileName);
            var contents = await reader.ReadToEndAsync();
            // Display file contents to text box field
            TbSourceTextDocument!.Text = contents;
            LblStatusBarContent = $"File: {_currentOpenFileName}";
            var displayName = fileInfo.Name;
            LblFileNameContent =
                displayName.Length > 50 ? $"{displayName[..25]}...{displayName[^15..]}" : displayName;
            var codeText = Opencc.ZhoCheck(contents);
            UpdateEncodeInfo(codeText);
        }
        catch (Exception)
        {
            TbSourceTextDocument!.Text = string.Empty;
            TbSourceTextDocument!.UndoStack.ClearAll();
            LblSourceCodeContent = string.Empty;
            LblStatusBarContent = "Error: Invalid file";
            //throw;
        }
    }

    private string GetCurrentConfig()
    {
        if (IsRbCustom) return SelectedItem![..SelectedItem!.IndexOf(' ')];

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
        LblTotalCharsContent = $"[ Chars: {TbSourceTextDocument!.Text!.Length:N0} ]";
    }

    #region Control Binding fields

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

    public string? LblTotalCharsContent
    {
        get => _lblTotalCharsContent;
        set => this.RaiseAndSetIfChanged(ref _lblTotalCharsContent, value);
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
            // LblDestinationCodeContent = _languagesInfo[1].Name;
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
            // LblDestinationCodeContent = _languagesInfo[2].Name;
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

    public bool IsCbPunctuation
    {
        get => _isCbPunctuation;
        set => this.RaiseAndSetIfChanged(ref _isCbPunctuation, value);
    }

    public bool IsCbConvertFilename
    {
        get => _isCbConvertFilename;
        set => this.RaiseAndSetIfChanged(ref _isCbConvertFilename, value);
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
}