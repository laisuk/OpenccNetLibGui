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

    /// <summary>
    /// Ensures the Windows system clipboard commits its current contents immediately.
    ///
    /// <para>
    /// On Windows, some clipboard operations use delayed rendering, meaning the actual
    /// clipboard data is not fully materialized until another application requests it.
    /// If the application providing the data exits too quickly, the clipboard may lose
    /// its contents.  
    /// </para>
    ///
    /// <para>
    /// Calling <c>OleFlushClipboard()</c> forces Windows to "take ownership" of the data
    /// immediately, making the clipboard behave similarly to Qt’s <c>QClipboard</c>,
    /// where the clipboard is automatically flushed and survives application exit.
    /// </para>
    ///
    /// <para>
    /// On Linux and macOS this method is a no-op, because their clipboard/pasteboard
    /// systems commit data eagerly and do not require flushing.
    /// </para>
    /// </summary>
    private static void FlushSystemClipboardIfNeeded()
    {
        // Only meaningful on Windows; no-op on other OSes
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            // HRESULT < 0 indicates failure, but clipboard data is already set so ignore.
            _ = OleFlushClipboard();
        }
        catch
        {
            // Swallow any platform or P/Invoke errors to avoid impacting application flow.
        }
    }

    /// <summary>
    /// Calls the native Win32 API <c>OleFlushClipboard()</c> from <c>ole32.dll</c>.
    ///
    /// <para>
    /// This function instructs OLE to fully render and commit the clipboard's contents,
    /// ensuring they persist even after the current process exits.  
    /// </para>
    /// </summary>
    /// <returns>
    /// An HRESULT value (0 for success). Negative values indicate failure, but callers
    /// typically ignore the result because clipboard contents are already placed.
    /// </returns>
    [DllImport("ole32.dll")]
    private static extern int OleFlushClipboard();

}