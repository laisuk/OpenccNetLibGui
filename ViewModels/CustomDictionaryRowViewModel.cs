using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using OpenccNetLib;
using OpenccNetLibGui.Services;
using ReactiveUI;

namespace OpenccNetLibGui.ViewModels;

public sealed class CustomDictionaryRowViewModel : ViewModelBase
{
    private readonly ITopLevelService _topLevelService;
    private readonly Func<DictionaryGeneratorContents> _contentsProvider;
    private DictSlot _selectedSlot;
    private CustomDictMode _selectedMode = CustomDictMode.Append;
    private string _dictionaryPath = string.Empty;

    public CustomDictionaryRowViewModel(ITopLevelService topLevelService, IReadOnlyList<DictSlot> availableSlots,
        IReadOnlyList<CustomDictMode> availableModes, Func<DictionaryGeneratorContents> contentsProvider,
        Action<CustomDictionaryRowViewModel> remove)
    {
        _topLevelService = topLevelService;
        AvailableSlots = availableSlots;
        AvailableModes = availableModes;
        _contentsProvider = contentsProvider;
        _selectedSlot = availableSlots[0];
        BrowseCommand = ReactiveCommand.CreateFromTask(BrowseAsync);
        RemoveCommand = ReactiveCommand.Create(() => remove(this));
    }

    public IReadOnlyList<DictSlot> AvailableSlots { get; }
    public IReadOnlyList<CustomDictMode> AvailableModes { get; }
    public ReactiveCommand<Unit, Unit> BrowseCommand { get; }
    public ReactiveCommand<Unit, Unit> RemoveCommand { get; }

    public DictSlot SelectedSlot
    {
        get => _selectedSlot;
        set => this.RaiseAndSetIfChanged(ref _selectedSlot, value);
    }

    public CustomDictMode SelectedMode
    {
        get => _selectedMode;
        set => this.RaiseAndSetIfChanged(ref _selectedMode, value);
    }

    public string DictionaryPath
    {
        get => _dictionaryPath;
        set => this.RaiseAndSetIfChanged(ref _dictionaryPath, value);
    }

    private async Task BrowseAsync()
    {
        var storageProvider = _topLevelService.GetMainWindow().StorageProvider;
        IStorageFolder? suggestedStartLocation = null;
        try
        {
            var directory = Path.GetDirectoryName(DictionaryGeneratorViewModel.ResolvePath(DictionaryPath));
            if (Directory.Exists(directory))
                suggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(new Uri(directory));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            // An invalid manually entered path should not prevent opening the picker.
        }

        var contents = _contentsProvider();
        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = contents.CustomFilePickerTitle,
            AllowMultiple = false,
            SuggestedStartLocation = suggestedStartLocation,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(contents.DictionaryTextFilesFilter) { Patterns = new[] { "*.txt" } },
                new FilePickerFileType(contents.AllFilesFilter) { Patterns = new[] { "*.*" } }
            }
        });
        if (result.Count > 0) DictionaryPath = result[0].Path.LocalPath;
    }
}