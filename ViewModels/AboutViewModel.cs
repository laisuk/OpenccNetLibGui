using System.Reactive;
using ReactiveUI;

namespace OpenccNetLibGui.ViewModels;

public sealed class AboutViewModel : ReactiveObject
{
    // Instance property required for XAML binding (do not make them static)
    public string AppName => "OpenccNetLibGui";

    public string Version =>
        typeof(AboutViewModel).Assembly
            .GetName().Version?.ToString() ?? "Unknown";

    public string Description =>
        "Open Chinese Simplified / Traditional Converter\nPowered by OpenccNetLib + Pdfium";

    public string PdfEngine => "Pdfium (native)";

    public ReactiveCommand<Unit, Unit> Close { get; }
        = ReactiveCommand.Create(() => { });

    // ✅ 保留 constructor，但唔做初始化
    public AboutViewModel()
    {
        // Intentionally empty
    }
}
