using System;
using System.Runtime.InteropServices;
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

        // Windows-only: force the clipboard contents to be rendered / owned by the OS
        FlushSystemClipboardIfNeeded();
    }

    public Window GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow ?? throw new NullReferenceException("Main window is null.");

        throw new InvalidOperationException(
            "Application is not running with a classic desktop style application lifetime.");
    }

    private static void FlushSystemClipboardIfNeeded()
    {
        // Only meaningful on Windows; no-op on other OSes
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            // HRESULT < 0 indicates failure, but usually we can ignore errors here
            _ = OleFlushClipboard();
        }
        catch
        {
            // Swallow any P/Invoke / platform errors; clipboard content is already set
        }
    }

    [DllImport("ole32.dll")]
    private static extern int OleFlushClipboard();
}