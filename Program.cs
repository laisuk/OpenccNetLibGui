using System;
using System.Reactive.Concurrency;
using Avalonia;
using Avalonia.Threading;
using ReactiveUI;

namespace OpenccNetLibGui;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    // private static AppBuilder BuildAvaloniaApp()
    // {
    //     return AppBuilder.Configure<App>()
    //         .UsePlatformDetect()
    //         .WithInterFont()
    //         .LogToTrace()
    //         .UseReactiveUI(_ => { });
    // }
    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .AfterSetup(_ =>
            {
                AvaloniaSynchronizationContext.InstallIfNeeded();
                RxSchedulers.MainThreadScheduler =
                    new SynchronizationContextScheduler(new AvaloniaSynchronizationContext());
            })
            .WithInterFont()
            .LogToTrace();
    }
}