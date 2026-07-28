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
using ReactiveUI.Reactive;

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
    private DictionaryGeneratorContents _contents = new();
    private string? _lastGeneratedOutputPath;
    private GenerationStatusKind _generationStatusKind;

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
    private IReadOnlyList<DictSlot> AvailableSlots { get; }
    private IReadOnlyList<CustomDictMode> AvailableModes { get; }
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

    public DictionaryGeneratorContents Contents
    {
        get => _contents;
        private set => this.RaiseAndSetIfChanged(ref _contents, value);
    }

    public void ApplyLanguage(DictionaryGeneratorContents? contents)
    {
        Contents = contents ?? new DictionaryGeneratorContents();
        GenerationStatus = _generationStatusKind switch
        {
            GenerationStatusKind.Generating => Contents.GeneratingStatus,
            GenerationStatusKind.Success when _lastGeneratedOutputPath is not null =>
                string.Format(Contents.GenerationSuccessFormat, Environment.NewLine, _lastGeneratedOutputPath),
            _ => GenerationStatus
        };
    }
    public static string ResolvePath(string path) => Path.GetFullPath(path, AppContext.BaseDirectory);

    private static IReadOnlyList<DictSlot> GetActiveSlots()
    {
        var preferredOrder = new[]
        {
            // Simplified
            DictSlot.STCharacters,
            DictSlot.STPhrases,
            DictSlot.STPunctuations,

            // Traditional
            DictSlot.TSCharacters,
            DictSlot.TSPhrases,
            DictSlot.TSPunctuations,

            // Taiwan
            DictSlot.TWVariants,
            DictSlot.TWVariantsRev,
            DictSlot.TWVariantsPhrases,
            DictSlot.TWVariantsRevPhrases,
            DictSlot.TWPhrases,
            DictSlot.TWPhrasesRev,

            // Hong Kong
            DictSlot.HKVariants,
            DictSlot.HKVariantsRev,
            DictSlot.HKVariantsPhrases,
            DictSlot.HKVariantsRevPhrases,
            DictSlot.HKPhrases,
            DictSlot.HKPhrasesRev,

            // Japanese
            DictSlot.JPSCharacters,
            DictSlot.JPSCharactersRev,
            DictSlot.JPSPhrases
        };

        var activeSlots = Enum.GetValues<DictSlot>()
            .Where(slot =>
                typeof(DictSlot)
                    .GetField(slot.ToString())?
                    .GetCustomAttribute<ObsoleteAttribute>() is null)
            .ToArray();

        return preferredOrder
            .Where(activeSlots.Contains)
            .Concat(activeSlots.Except(preferredOrder))
            .ToArray();
    }

    private void AddCustomDictionary() => CustomDictionaries.Add(new CustomDictionaryRowViewModel(
        _topLevelService, AvailableSlots, AvailableModes, () => Contents,
        row => CustomDictionaries.Remove(row)));

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
            Title = isBaseDirectory ? Contents.BaseDirectoryPickerTitle : Contents.OutputDirectoryPickerTitle,
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
            _generationStatusKind = GenerationStatusKind.Generating;
            GenerationStatus = Contents.GeneratingStatus;
            var outputPath = await Task.Run(() => _generatorService.Generate(request));
            _lastGeneratedOutputPath = outputPath;
            _generationStatusKind = GenerationStatusKind.Success;
            GenerationStatus = string.Format(Contents.GenerationSuccessFormat, Environment.NewLine, outputPath);
        }
        catch (Exception exception) when (exception is ArgumentException or DirectoryNotFoundException or
                                              FileNotFoundException or UnauthorizedAccessException or IOException or
                                              InvalidDataException or FormatException)
        {
            await ShowErrorAsync(exception.Message);
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(string.Format(Contents.GenerationFailedFormat, exception.Message));
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private DictionaryGenerationRequest CreateValidatedRequest(DictionaryOutputFormat format)
    {
        if (string.IsNullOrWhiteSpace(BaseDictionaryDirectory))
            throw new ArgumentException(Contents.BaseDirectoryRequired);
        var baseDirectory = ResolvePath(BaseDictionaryDirectory.Trim());
        if (!Directory.Exists(baseDirectory))
            throw new DirectoryNotFoundException(string.Format(Contents.BaseDirectoryNotFoundFormat, baseDirectory));
        if (string.IsNullOrWhiteSpace(OutputDirectory))
            throw new ArgumentException(Contents.OutputDirectoryRequired);
        var outputDirectory = ResolvePath(OutputDirectory.Trim());
        if (!Directory.Exists(outputDirectory))
            throw new DirectoryNotFoundException(string.Format(Contents.OutputDirectoryNotFoundFormat, outputDirectory));

        var activeSlots = AvailableSlots.ToHashSet();
        var activeModes = AvailableModes.ToHashSet();
        var customRequests = new List<CustomDictionaryRequest>(CustomDictionaries.Count);
        for (var index = 0; index < CustomDictionaries.Count; index++)
        {
            var row = CustomDictionaries[index];
            var rowNumber = index + 1;
            if (!activeSlots.Contains(row.SelectedSlot))
                throw new ArgumentException(string.Format(Contents.UnsupportedSlotFormat, rowNumber, row.SelectedSlot));
            if (!activeModes.Contains(row.SelectedMode))
                throw new ArgumentException(string.Format(Contents.UnsupportedModeFormat, rowNumber, row.SelectedMode));
            if (string.IsNullOrWhiteSpace(row.DictionaryPath))
                throw new ArgumentException(string.Format(Contents.RowFileRequiredFormat, rowNumber));
            var path = ResolvePath(row.DictionaryPath.Trim());
            if (!File.Exists(path))
                throw new FileNotFoundException(string.Format(Contents.RowFileNotFoundFormat, rowNumber, path), path);
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
        _generationStatusKind = GenerationStatusKind.Error;
        await MessageBox.Show(message, Contents.MessageBoxTitle, _topLevelService.GetMainWindow());
    }
    private enum GenerationStatusKind
    {
        None,
        Generating,
        Success,
        Error
    }
}