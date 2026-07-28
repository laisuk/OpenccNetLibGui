using System.Reactive;
using OpenccNetLibGui.Helpers;
using ReactiveUI.Reactive;

namespace OpenccNetLibGui.ViewModels;

public sealed class AboutViewModel : ViewModelBase
{
    // Instance property required for XAML binding (do not make them static)
    public string AppName => "OpenccNetLibGui";

    public string Version =>
        typeof(AboutViewModel).Assembly
            .GetName().Version?.ToString() ?? "Unknown";

    public string Description =>
        "Open Chinese Simplified / Traditional Converter\nPowered by OpenccNetLib + Pdfium";

    public string PdfEngine => "Pdfium (native)";

    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    public AboutViewModel()
    {
        CloseCommand = ReactiveCommand.Create(() => { });
        TrackSubscription(ReactiveCommandExceptionObserver.Subscribe(
            _ => { },
            (nameof(CloseCommand), CloseCommand.ThrownExceptions)));
    }
}
