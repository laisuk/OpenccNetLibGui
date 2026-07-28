using System;
using System.Diagnostics;
using Avalonia.Input;
using Avalonia.Controls;
using OpenccNetLibGui.ViewModels;

namespace OpenccNetLibGui.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();

        Opened += OnOpened;
        Closed += OnClosed;
    }

    private IDisposable? _closeSubscription;

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is AboutViewModel vm)
        {
            _closeSubscription = vm.CloseCommand.Subscribe(_ => Close());
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closeSubscription?.Dispose();
        _closeSubscription = null;

        if (DataContext is IDisposable disposable)
            disposable.Dispose();
        DataContext = null;

        Opened -= OnOpened;
        Closed -= OnClosed;
    }

    private void OnGitHubLinkClicked(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;

        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/laisuk/OpenccNetLibGui",
            UseShellExecute = true
        });
    }
}