using System.Reactive;
using OpenccNetLibGui.Helpers;
using ReactiveUI.Reactive;

namespace OpenccNetLibGui.ViewModels;

public sealed class AboutViewModel : ViewModelBase
{
    // Instance property required for XAML binding (do not make them static)
    public static string AppName => "OpenccNetLibGui";

    public static string Version =>
        typeof(AboutViewModel).Assembly
            .GetName().Version?.ToString() ?? "Unknown";

    public static string Description =>
        "Open Chinese Simplified / Traditional Converter\nPowered by OpenccNetLib + Pdfium";

    public string PdfEngine { get; }

    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    public AboutViewModel(string? pdfiumVersion)
    {
        PdfEngine = string.IsNullOrWhiteSpace(pdfiumVersion)
            ? "Pdfium (native)"
            : $"Pdfium (native) · {pdfiumVersion}";

        CloseCommand = ReactiveCommand.Create(() => { });
        TrackSubscription(ReactiveCommandExceptionObserver.Subscribe(
            _ => { },
            (nameof(CloseCommand), CloseCommand.ThrownExceptions)));
    }
}
