using System.Diagnostics;
// using System.Reactive.Disposables.Fluent;
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

        // this.WhenActivated(d =>
        // {
        //     if (ViewModel is { } vm)
        //         vm.CloseCommand.Subscribe(_ => Close()).DisposeWith(d);
        // });
        this.WhenActivated(d =>
        {
            if (ViewModel is { } vm)
                d.Add(vm.CloseCommand.Subscribe(_ => Close()));
        });
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