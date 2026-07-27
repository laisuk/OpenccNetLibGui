using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using OpenccNetLib;
using OpenccNetLibGui.Services;
using OpenccNetLibGui.Views;
using ReactiveUI;

namespace OpenccNetLibGui.ViewModels;

public sealed class DictionaryGeneratorViewModel : ViewModelBase
{
    private readonly ITopLevelService _topLevelService;
    private readonly IDictionaryGeneratorService _generatorService;
    private string _baseDictionaryDirectory = "dicts";
    private string _outputDirectory = AppContext.BaseDirectory;
    private bool _isGenerating;
    private string _generationStatus = string.Empty;
    private bool _hasGenerationError;
    private bool _readableUnicodeJson;

    public DictionaryGeneratorViewModel(ITopLevelService topLevelService, IDictionaryGeneratorService generatorService)
    {
        _topLevelService = topLevelService;
        _generatorService = generatorService;
        AvailableSlots = GetActiveSlots();
        AvailableModes = Enum.GetValues<CustomDictMode>();
        AddCustomDictionaryCommand = ReactiveCommand.Create(AddCustomDictionary);
        BrowseBaseDirectoryCommand = ReactiveCommand.CreateFromTask(() => BrowseDirectoryAsync(true));
        BrowseOutputDirectoryCommand = ReactiveCommand.CreateFromTask(() => BrowseDirectoryAsync(false));
        GenerateZstdCommand = ReactiveCommand.CreateFromTask(() => GenerateAsync(DictionaryOutputFormat.Zstd));
        GenerateCborCommand = ReactiveCommand.CreateFromTask(() => GenerateAsync(DictionaryOutputFormat.Cbor));
        GenerateJsonCommand = ReactiveCommand.CreateFromTask(() => GenerateAsync(DictionaryOutputFormat.Json));
    }

    public ObservableCollection<CustomDictionaryRowViewModel> CustomDictionaries { get; } = new();
    public IReadOnlyList<DictSlot> AvailableSlots { get; }
    public IReadOnlyList<CustomDictMode> AvailableModes { get; }
    public ReactiveCommand<Unit, Unit> AddCustomDictionaryCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseBaseDirectoryCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseOutputDirectoryCommand { get; }
    public ReactiveCommand<Unit, Unit> GenerateZstdCommand { get; }
    public ReactiveCommand<Unit, Unit> GenerateCborCommand { get; }
    public ReactiveCommand<Unit, Unit> GenerateJsonCommand { get; }

    public string BaseDictionaryDirectory
    {
        get => _baseDictionaryDirectory;
        set => this.RaiseAndSetIfChanged(ref _baseDictionaryDirectory, value);
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        set => this.RaiseAndSetIfChanged(ref _outputDirectory, value);
    }

    public bool IsGenerating
    {
        get => _isGenerating;
        private set => this.RaiseAndSetIfChanged(ref _isGenerating, value);
    }

    public string GenerationStatus
    {
        get => _generationStatus;
        private set => this.RaiseAndSetIfChanged(ref _generationStatus, value);
    }

    public bool HasGenerationError
    {
        get => _hasGenerationError;
        private set => this.RaiseAndSetIfChanged(ref _hasGenerationError, value);
    }

    public bool ReadableUnicodeJson
    {
        get => _readableUnicodeJson;
        set => this.RaiseAndSetIfChanged(ref _readableUnicodeJson, value);
    }

    public static string ResolvePath(string path) => Path.GetFullPath(path, AppContext.BaseDirectory);

    public static IReadOnlyList<DictSlot> GetActiveSlots() => Enum.GetValues<DictSlot>()
        .Where(slot => typeof(DictSlot).GetField(slot.ToString())?.GetCustomAttribute<ObsoleteAttribute>() is null)
        .ToArray();

    private void AddCustomDictionary() => CustomDictionaries.Add(new CustomDictionaryRowViewModel(
        _topLevelService, AvailableSlots, AvailableModes, row => CustomDictionaries.Remove(row)));

    private async Task BrowseDirectoryAsync(bool isBaseDirectory)
    {
        if (IsGenerating) return;
        var storageProvider = _topLevelService.GetMainWindow().StorageProvider;
        IStorageFolder? suggestedStartLocation = null;
        var currentPath = isBaseDirectory ? BaseDictionaryDirectory : OutputDirectory;
        try
        {
            var resolvedPath = ResolvePath(currentPath);
            if (Directory.Exists(resolvedPath))
                suggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(new Uri(resolvedPath));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            // An invalid manually entered path should not prevent opening the picker.
        }

        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = isBaseDirectory ? "Select Base Dictionary Directory" : "Select Output Directory",
            SuggestedStartLocation = suggestedStartLocation
        });
        if (result.Count == 0) return;
        if (isBaseDirectory) BaseDictionaryDirectory = result[0].Path.LocalPath;
        else OutputDirectory = result[0].Path.LocalPath;
    }

    private async Task GenerateAsync(DictionaryOutputFormat format)
    {
        if (IsGenerating) return;
        try
        {
            var request = CreateValidatedRequest(format);
            IsGenerating = true;
            HasGenerationError = false;
            GenerationStatus = "Generating dictionary…";
            var outputPath = await Task.Run(() => _generatorService.Generate(request));
            GenerationStatus = $"Dictionary generated successfully:{Environment.NewLine}{outputPath}";
        }
        catch (Exception exception) when (exception is ArgumentException or DirectoryNotFoundException or
                                              FileNotFoundException or UnauthorizedAccessException or IOException or
                                              InvalidDataException or FormatException)
        {
            await ShowErrorAsync(exception.Message);
        }
        catch (Exception exception)
        {
            await ShowErrorAsync($"Dictionary generation failed: {exception.Message}");
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private DictionaryGenerationRequest CreateValidatedRequest(DictionaryOutputFormat format)
    {
        if (string.IsNullOrWhiteSpace(BaseDictionaryDirectory))
            throw new ArgumentException("Base dictionary directory is required.");
        var baseDirectory = ResolvePath(BaseDictionaryDirectory.Trim());
        if (!Directory.Exists(baseDirectory))
            throw new DirectoryNotFoundException($"Base dictionary directory not found: {baseDirectory}");
        if (string.IsNullOrWhiteSpace(OutputDirectory))
            throw new ArgumentException("Output directory is required.");
        var outputDirectory = ResolvePath(OutputDirectory.Trim());
        if (!Directory.Exists(outputDirectory))
            throw new DirectoryNotFoundException($"Output directory not found: {outputDirectory}");

        var activeSlots = AvailableSlots.ToHashSet();
        var activeModes = AvailableModes.ToHashSet();
        var customRequests = new List<CustomDictionaryRequest>(CustomDictionaries.Count);
        for (var index = 0; index < CustomDictionaries.Count; index++)
        {
            var row = CustomDictionaries[index];
            var rowNumber = index + 1;
            if (!activeSlots.Contains(row.SelectedSlot))
                throw new ArgumentException($"Custom dictionary row {rowNumber}: unsupported dictionary slot.");
            if (!activeModes.Contains(row.SelectedMode))
                throw new ArgumentException($"Custom dictionary row {rowNumber}: unsupported dictionary mode.");
            if (string.IsNullOrWhiteSpace(row.DictionaryPath))
                throw new ArgumentException($"Custom dictionary row {rowNumber}: file path is required.");
            var path = ResolvePath(row.DictionaryPath.Trim());
            if (!File.Exists(path))
                throw new FileNotFoundException($"Custom dictionary row {rowNumber}: file not found: {path}", path);
            customRequests.Add(new CustomDictionaryRequest(row.SelectedSlot, row.SelectedMode, path));
        }

        return new DictionaryGenerationRequest(
            baseDirectory,
            outputDirectory,
            customRequests,
            format,
            format == DictionaryOutputFormat.Json && ReadableUnicodeJson);
    }

    private async Task ShowErrorAsync(string message)
    {
        HasGenerationError = true;
        GenerationStatus = message;
        await MessageBox.Show(message, "Dictionary Generation", _topLevelService.GetMainWindow());
    }
}