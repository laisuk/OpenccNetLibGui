using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using OpenccNetLibGui.Services;
using ReactiveUI.Reactive;

namespace OpenccNetLibGui.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private static readonly string[] ThemeModeValues = { "System", "Light", "Dark" };
    private static readonly string[] GlobalDictionaryValues = { "zstd", "dicts", "json", "cbor" };

    private readonly ITopLevelService? _topLevelService;
    private readonly LanguageSettingsService? _languageSettingsService;
    private readonly LanguageSettings? _languageSettings;
    private int _selectedThemeModeIndex;
    private string _selectedThemeMode = "System";
    private ThemeModeOption? _selectedThemeModeOption;
    private int _selectedUiScale = 100;
    private FontFamily _editorFontFamily;
    private double _editorFontSize = 14;
    private GlobalDictionaryOption? _selectedGlobalDictionaryOption;
    private Language _language = new();

    public SettingsViewModel()
        : this((null, null))
    {
    }

    public SettingsViewModel(ITopLevelService topLevelService, LanguageSettingsService languageSettingsService)
        : this((topLevelService, languageSettingsService))
    {
    }

    private SettingsViewModel((ITopLevelService? TopLevelService, LanguageSettingsService? SettingsService) services)
    {
        _topLevelService = services.TopLevelService;
        _languageSettingsService = services.SettingsService;
        _languageSettings = services.SettingsService?.LanguageSettings;

        foreach (var value in ThemeModeValues)
            ThemeModeOptions.Add(new ThemeModeOption(value, value));
        foreach (var value in GlobalDictionaryValues)
            GlobalDictionaryOptions.Add(new GlobalDictionaryOption(value, value));

        if (_languageSettings is not null)
        {
            _selectedThemeMode = NormalizeThemeMode(_languageSettings.ThemeMode);
            _selectedThemeModeIndex = GetThemeModeIndex(_selectedThemeMode);
            _selectedUiScale = NormalizeUiScale(_languageSettings.UiScale);
            _editorFontFamily = ResolveEditorFontFamily(_languageSettings.EditorFont);
            _editorFontSize = NormalizeEditorFontSize(_languageSettings.EditorFontSize);
            _selectedGlobalDictionaryOption = FindDictionaryOption(
                NormalizeGlobalDictionary(_languageSettings.Dictionary));
            ApplyThemeMode(_selectedThemeMode);
        }
        else
        {
            _selectedThemeModeOption = ThemeModeOptions[0];
            _selectedGlobalDictionaryOption = GlobalDictionaryOptions[0];
            _editorFontFamily = ResolveEditorFontFamily(null);
        }

        _selectedThemeModeOption ??= FindThemeOption(_selectedThemeMode);
        _selectedGlobalDictionaryOption ??= GlobalDictionaryOptions[0];
        SaveLanguageSettingsCommand = ReactiveCommand.Create(SaveLanguageSettings);
        ResetWindowSizeCommand = ReactiveCommand.Create(ResetWindowSize);
    }

    public event EventHandler? SettingsSaved;

    public ObservableCollection<ThemeModeOption> ThemeModeOptions { get; } = new();
    public ObservableCollection<GlobalDictionaryOption> GlobalDictionaryOptions { get; } = new();
    public IReadOnlyList<FontFamily> SystemFonts { get; } = FontManager.Current.SystemFonts;
    public IReadOnlyList<int> UiScaleOptions { get; } = new[] { 100, 125, 150 };

    public ReactiveCommand<Unit, Unit> SaveLanguageSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetWindowSizeCommand { get; }

    public bool IsSettingsDirty => _languageSettingsService?.IsDirty ?? false;
    public double UiScaleFactor => SelectedUiScale / 100d;
    public double WindowWidth => _languageSettings?.WindowWidth ?? LanguageSettingsService.DefaultWindowWidth;
    public double WindowHeight => _languageSettings?.WindowHeight ?? LanguageSettingsService.DefaultWindowHeight;

    public int SelectedUiScale
    {
        get => _selectedUiScale;
        set
        {
            var normalized = NormalizeUiScale(value);
            if (_selectedUiScale == normalized)
                return;
            this.RaiseAndSetIfChanged(ref _selectedUiScale, normalized);
            this.RaisePropertyChanged(nameof(UiScaleFactor));
            if (_languageSettings is null || _languageSettings.UiScale == normalized)
                return;
            _languageSettings.UiScale = normalized;
            RefreshDirtyState();
        }
    }

    public string SelectedThemeMode
    {
        get => _selectedThemeMode;
        set
        {
            var normalized = NormalizeThemeMode(value);
            if (string.Equals(_selectedThemeMode, normalized, StringComparison.Ordinal))
                return;
            this.RaiseAndSetIfChanged(ref _selectedThemeMode, normalized);
            SetSelectedThemeModeIndex(GetThemeModeIndex(normalized));
            SetSelectedThemeModeOption(normalized);
            ApplyThemeMode(normalized);
            if (_languageSettings is null ||
                string.Equals(_languageSettings.ThemeMode, normalized, StringComparison.Ordinal))
                return;
            _languageSettings.ThemeMode = normalized;
            RefreshDirtyState();
        }
    }

    public int SelectedThemeModeIndex
    {
        get => _selectedThemeModeIndex;
        set
        {
            if (value < 0)
                return;
            var normalized = Math.Clamp(value, 0, ThemeModeValues.Length - 1);
            if (_selectedThemeModeIndex == normalized)
                return;
            this.RaiseAndSetIfChanged(ref _selectedThemeModeIndex, normalized);
            SelectedThemeMode = ThemeModeValues[normalized];
        }
    }

    public ThemeModeOption? SelectedThemeModeOption
    {
        get => _selectedThemeModeOption;
        set
        {
            var option = value ?? ThemeModeOptions[0];
            if (ReferenceEquals(_selectedThemeModeOption, option))
                return;
            this.RaiseAndSetIfChanged(ref _selectedThemeModeOption, option);
            SelectedThemeMode = option.Value;
            this.RaisePropertyChanged(nameof(SelectedThemeModeIndex));
        }
    }

    public FontFamily EditorFontFamily
    {
        get => _editorFontFamily;
        set
        {
            if (_editorFontFamily == value)
                return;
            this.RaiseAndSetIfChanged(ref _editorFontFamily, value);
            if (_languageSettings is null)
                return;
            _languageSettings.EditorFont = value.Name;
            RefreshDirtyState();
        }
    }

    public double EditorFontSize
    {
        get => _editorFontSize;
        set
        {
            var normalized = NormalizeEditorFontSize(value);
            if (Math.Abs(_editorFontSize - normalized) < 0.001)
                return;
            this.RaiseAndSetIfChanged(ref _editorFontSize, normalized);
            this.RaisePropertyChanged(nameof(EditorFontSizeValue));
            if (_languageSettings is null)
                return;
            _languageSettings.EditorFontSize = normalized;
            RefreshDirtyState();
        }
    }

    public decimal EditorFontSizeValue
    {
        get => (decimal)EditorFontSize;
        set => EditorFontSize = (double)value;
    }

    public GlobalDictionaryOption? SelectedGlobalDictionaryOption
    {
        get => _selectedGlobalDictionaryOption;
        set
        {
            var option = value ?? GlobalDictionaryOptions[0];
            if (ReferenceEquals(_selectedGlobalDictionaryOption, option))
                return;
            this.RaiseAndSetIfChanged(ref _selectedGlobalDictionaryOption, option);
            if (_languageSettings is null ||
                string.Equals(_languageSettings.Dictionary, option.Value, StringComparison.Ordinal))
                return;
            _languageSettings.Dictionary = option.Value;
            RefreshDirtyState();
        }
    }

    public void SetGlobalDictionaryAfterStartupFallback(string value)
    {
        var normalized = NormalizeGlobalDictionary(value);
        if (_languageSettings is not null)
            _languageSettings.Dictionary = normalized;
        var option = FindDictionaryOption(normalized);
        if (!ReferenceEquals(_selectedGlobalDictionaryOption, option))
            this.RaiseAndSetIfChanged(ref _selectedGlobalDictionaryOption, option,
                nameof(SelectedGlobalDictionaryOption));
        else
            this.RaisePropertyChanged(nameof(SelectedGlobalDictionaryOption));
        RefreshDirtyState();
    }

    public void PersistWindowSize(double width, double height)
    {
        if (_languageSettingsService is null || _languageSettings is null)
            return;
        if (!double.IsFinite(width) || !double.IsFinite(height) ||
            width < LanguageSettingsService.MinimumWindowWidth ||
            height < LanguageSettingsService.MinimumWindowHeight ||
            width > int.MaxValue || height > int.MaxValue)
            return;
        _languageSettings.WindowWidth = (int)Math.Round(width);
        _languageSettings.WindowHeight = (int)Math.Round(height);
        _languageSettingsService.SaveDiffOnly();
        this.RaisePropertyChanged(nameof(WindowWidth));
        this.RaisePropertyChanged(nameof(WindowHeight));
        RefreshDirtyState();
    }

    public void RefreshDirtyState() => this.RaisePropertyChanged(nameof(IsSettingsDirty));

    public void ApplyLanguage(Language language)
    {
        _language = language;
        ConversionSettingsContent = ValueOrFallback(language.ConversionSettingsContent, "Conversion Settings");
        EditorFontContent = ValueOrFallback(language.EditorFontContent, "Editor Font");
        EditorFontSizeContent = ValueOrFallback(language.EditorFontSizeContent, "Font Size");
        GlobalDictionaryContent = ValueOrFallback(language.GlobalDictionaryContent, "Global Conversion Dictionary");
        UiLanguageContent = ValueOrFallback(language.UiLanguageContent, "UI Language");
        UiScaleContent = ValueOrFallback(language.UiScaleContent, "UI Scale");
        ThemeModeContent = ValueOrFallback(language.ThemeModeContent, "Theme Mode");
        ResetWindowSizeContent = ValueOrFallback(language.ResetWindowSizeContent, "Reset Window Size");
        BtnSaveAdvancedSettingsContent = ValueOrFallback(language.BtnSaveAdvancedSettingsContent,
            "Save Advanced Settings");
        UnsavedChangesContent = ValueOrFallback(language.UnsavedChangesContent, "Unsaved changes");
        AllSettingsSavedContent = ValueOrFallback(language.AllSettingsSavedContent, "All settings saved");
        RefreshThemeModeOptionLabels(language);
        RefreshGlobalDictionaryOptionLabels(language);
        this.RaisePropertyChanged(nameof(ConversionSettingsContent));
        this.RaisePropertyChanged(nameof(EditorFontContent));
        this.RaisePropertyChanged(nameof(EditorFontSizeContent));
        this.RaisePropertyChanged(nameof(GlobalDictionaryContent));
        this.RaisePropertyChanged(nameof(UiLanguageContent));
        this.RaisePropertyChanged(nameof(UiScaleContent));
        this.RaisePropertyChanged(nameof(ThemeModeContent));
        this.RaisePropertyChanged(nameof(ResetWindowSizeContent));
        this.RaisePropertyChanged(nameof(BtnSaveAdvancedSettingsContent));
        this.RaisePropertyChanged(nameof(UnsavedChangesContent));
        this.RaisePropertyChanged(nameof(AllSettingsSavedContent));
        this.RaisePropertyChanged(nameof(ThemeModeHint));
        this.RaisePropertyChanged(nameof(GlobalDictionaryHint));
        this.RaisePropertyChanged(nameof(SaveAdvancedSettingsHint));
        this.RaisePropertyChanged(nameof(SelectedThemeModeOption));
        this.RaisePropertyChanged(nameof(SelectedGlobalDictionaryOption));
    }

    public string ConversionSettingsContent { get; private set; } = "Conversion Settings";
    public string EditorFontContent { get; private set; } = "Editor Font";
    public string EditorFontSizeContent { get; private set; } = "Font Size";
    public string GlobalDictionaryContent { get; private set; } = "Global Conversion Dictionary";
    public string UiLanguageContent { get; private set; } = "UI Language";
    public string UiScaleContent { get; private set; } = "UI Scale";
    public string ThemeModeContent { get; private set; } = "Theme Mode";
    public string ResetWindowSizeContent { get; private set; } = "Reset Window Size";
    public string BtnSaveAdvancedSettingsContent { get; private set; } = "Save Advanced Settings";
    public string UnsavedChangesContent { get; private set; } = "Unsaved changes";
    public string AllSettingsSavedContent { get; private set; } = "All settings saved";
    public string ThemeModeHint => GetHint("themeModeHint", "System follows the operating system theme.");

    public string GlobalDictionaryHint => GetHint("globalDictionaryHint",
        "Changes take effect after restarting the application.");

    public string SaveAdvancedSettingsHint => GetHint("saveAdvancedSettingsHint",
        "Writes UserLanguageSettings.json (advanced users only)");

    public static string NormalizeGlobalDictionary(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "dicts" => "dicts",
        "json" => "json",
        "cbor" => "cbor",
        _ => "zstd"
    };

    private void SetSelectedThemeModeIndex(int index)
    {
        var normalized = Math.Clamp(index, 0, ThemeModeValues.Length - 1);
        if (_selectedThemeModeIndex != normalized)
            this.RaiseAndSetIfChanged(ref _selectedThemeModeIndex, normalized,
                nameof(SelectedThemeModeIndex));
        else
            this.RaisePropertyChanged(nameof(SelectedThemeModeIndex));
        SetSelectedThemeModeOption(ThemeModeValues[normalized]);
    }

    private void SetSelectedThemeModeOption(string value)
    {
        var option = FindThemeOption(value);
        if (!ReferenceEquals(_selectedThemeModeOption, option))
            this.RaiseAndSetIfChanged(ref _selectedThemeModeOption, option,
                nameof(SelectedThemeModeOption));
    }

    private ThemeModeOption FindThemeOption(string value) => ThemeModeOptions.First(option =>
        string.Equals(option.Value, NormalizeThemeMode(value), StringComparison.Ordinal));

    private GlobalDictionaryOption FindDictionaryOption(string value) => GlobalDictionaryOptions.First(option =>
        string.Equals(option.Value, NormalizeGlobalDictionary(value), StringComparison.Ordinal));

    private static string NormalizeThemeMode(string? value) => value switch
    {
        "Light" => "Light",
        "Dark" => "Dark",
        _ => "System"
    };

    private static int GetThemeModeIndex(string? value)
    {
        var index = Array.IndexOf(ThemeModeValues, NormalizeThemeMode(value));
        return index >= 0 ? index : 0;
    }

    private static int NormalizeUiScale(int value) => value is 100 or 125 or 150 ? value : 100;
    private static double NormalizeEditorFontSize(double value) => Math.Clamp(value <= 0 ? 14 : value, 8, 72);

    private FontFamily ResolveEditorFontFamily(string? fontName)
    {
        if (!string.IsNullOrWhiteSpace(fontName))
        {
            var configured = SystemFonts.FirstOrDefault(font =>
                string.Equals(font.Name, fontName, StringComparison.OrdinalIgnoreCase));
            if (configured is not null)
                return configured;
        }

        return SystemFonts.FirstOrDefault(font =>
                   string.Equals(font.Name, "Consolas", StringComparison.OrdinalIgnoreCase))
               ?? FontFamily.Default;
    }

    private static void ApplyThemeMode(string value)
    {
        if (Application.Current is not { } app)
            return;
        app.RequestedThemeVariant = NormalizeThemeMode(value) switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    private void ResetWindowSize()
    {
        if (_topLevelService?.GetMainWindow() is not { } window)
            return;
        window.WindowState = WindowState.Normal;
        window.Width = LanguageSettingsService.DefaultWindowWidth;
        window.Height = LanguageSettingsService.DefaultWindowHeight;
        Dispatcher.UIThread.Post(() => CenterWindow(window));
        PersistWindowSize(LanguageSettingsService.DefaultWindowWidth, LanguageSettingsService.DefaultWindowHeight);
    }

    private static void CenterWindow(Window window)
    {
        var screen = window.Screens.ScreenFromWindow(window);
        if (screen is null)
            return;
        var scaling = window.RenderScaling;
        var area = screen.WorkingArea;
        var width = (int)Math.Round(LanguageSettingsService.DefaultWindowWidth * scaling);
        var height = (int)Math.Round(LanguageSettingsService.DefaultWindowHeight * scaling);
        window.Position = new PixelPoint(
            area.X + Math.Max(0, (area.Width - width) / 2),
            area.Y + Math.Max(0, (area.Height - height) / 2));
    }

    private void SaveLanguageSettings()
    {
        if (_languageSettingsService is null)
            return;
        _languageSettingsService.SaveDiffOnly();
        RefreshDirtyState();
        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshThemeModeOptionLabels(Language language)
    {
        for (var i = 0; i < ThemeModeOptions.Count; i++)
            ThemeModeOptions[i].Content = GetListItem(language.ThemeModeSelectionContent, i, ThemeModeValues[i]);
    }

    private void RefreshGlobalDictionaryOptionLabels(Language language)
    {
        foreach (var option in GlobalDictionaryOptions)
        {
            var key = option.Value == "zstd" ? "default" : option.Value;
            option.Content = language.Runtimes.Dictionaries.TryGetValue(key, out var label) &&
                             !string.IsNullOrWhiteSpace(label)
                ? label
                : option.Value switch
                {
                    "zstd" => "Default dictionary",
                    "dicts" => "Folder [dicts] dictionary",
                    "json" => "JSON dictionary",
                    "cbor" => "CBOR dictionary",
                    _ => option.Value
                };
        }
    }

    private string GetHint(string key, string fallback) =>
        _language.Hints.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static string GetListItem(IReadOnlyList<string>? values, int index, string fallback) =>
        values is not null && index >= 0 && index < values.Count && !string.IsNullOrWhiteSpace(values[index])
            ? values[index]
            : fallback;

    private static string ValueOrFallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    public sealed class ThemeModeOption : ReactiveObject
    {
        private string _content;

        public ThemeModeOption(string value, string content)
        {
            Value = value;
            _content = content;
        }

        internal string Value { get; }

        public string Content
        {
            get => _content;
            set => this.RaiseAndSetIfChanged(ref _content, value);
        }

        public override string ToString() => Content;
    }

    public sealed class GlobalDictionaryOption : ReactiveObject
    {
        private string _content;

        public GlobalDictionaryOption(string value, string content)
        {
            Value = value;
            _content = content;
        }

        internal string Value { get; }

        public string Content
        {
            get => _content;
            set => this.RaiseAndSetIfChanged(ref _content, value);
        }
    }
}