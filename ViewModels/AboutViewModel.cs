using System.Reactive;
using ReactiveUI;

namespace OpenccNetLibGui.ViewModels;

public sealed class AboutViewModel : ReactiveObject
{
    public static string AppName => "OpenccNetLibGui";

    public static string Version =>
        typeof(AboutViewModel).Assembly
            .GetName().Version?.ToString() ?? "Unknown";

    public static string Description =>
        "Chinese Simplified / Traditional Converter\nPowered by OpenccNetLib + Pdfium";

    public static string PdfEngine => "Pdfium (native)";

    // public ReactiveCommand<Unit, Unit> OpenGitHub { get; }
    //     = ReactiveCommand.Create(() =>
    //     {
    //         Process.Start(new ProcessStartInfo
    //         {
    //             FileName = "https://github.com/laisuk/OpenccNetLibGui",
    //             UseShellExecute = true
    //         });
    //     });

    public ReactiveCommand<Unit, Unit> Close { get; }
        = ReactiveCommand.Create(() => { });

    // ✅ 保留 constructor，但唔做初始化
    public AboutViewModel()
    {
        // Intentionally empty
    }
}