using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace OpenccNetLibGui.Services;

public interface ITopLevelService
{
    Task<string> GetClipboardTextAsync();
    Task SetClipboardTextAsync(string text);
    Window GetMainWindow();
}

public class TopLevelService : ITopLevelService
{
    public async Task<string> GetClipboardTextAsync()
    {
        var clipboard = GetMainWindow().Clipboard;
        if (clipboard != null) return (await clipboard.TryGetTextAsync())!;

        return string.Empty;
    }

    public async Task SetClipboardTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var clipboard = GetMainWindow().Clipboard;
        if (clipboard != null) await clipboard.SetTextAsync(text);
    }

    public Window GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow ?? throw new NullReferenceException("Main window is null.");

        throw new InvalidOperationException(
            "Application is not running with a classic desktop style application lifetime.");
    }
}