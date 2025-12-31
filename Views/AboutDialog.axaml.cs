using System.Diagnostics;
using System.Reactive.Disposables.Fluent;
using Avalonia.Input;
using AvaloniaEdit.Utils;
using OpenccNetLibGui.ViewModels;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace OpenccNetLibGui.Views;

public partial class AboutDialog : ReactiveWindow<AboutViewModel>
{
    public AboutDialog()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            if (ViewModel is { } vm)
                vm.Close.Subscribe(_ => Close()).DisposeWith(d);
        });
    }

    private void OnGitHubLinkClicked(object? sender, PointerPressedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/laisuk/OpenccNetLibGui",
            UseShellExecute = true
        });
    }
}