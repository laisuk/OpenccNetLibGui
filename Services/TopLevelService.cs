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
        if (clipboard is null)
            return string.Empty;

        // TryGetTextAsync can return null
        var text = await clipboard.TryGetTextAsync();
        return text ?? string.Empty;
    }

    public async Task SetClipboardTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var clipboard = GetMainWindow().Clipboard;
        if (clipboard is null)
            return;

        await clipboard.SetTextAsync(text);

        // Extra safety:
        // Docs say SetTextAsync flushes, but in practice we've seen cases where
        // the clipboard is empty if the app exits immediately on Windows.
        // FlushAsync is Windows-only and a no-op on other platforms.
        try
        {
            await clipboard.FlushAsync();
        }
        catch
        {
            // Ignore failures; clipboard already has best-effort data.
        }
    }

    public Window GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow ?? throw new NullReferenceException("Main window is null.");

        throw new InvalidOperationException(
            "Application is not running with a classic desktop style application lifetime.");
    }
}