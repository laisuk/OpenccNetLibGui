using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using OpenccNetLibGui.Services;
using OpenccNetLibGui.ViewModels;
using OpenccNetLibGui.Views;
using OpenccNetLib;

namespace OpenccNetLibGui;

public class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        // Register LanguageSettingsService with the path to the settings file
        services.AddSingleton<LanguageSettingsService>(_ =>
            new LanguageSettingsService(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LanguageSettings.json")));

        services.AddSingleton<ITopLevelService, TopLevelService>();
        services.AddSingleton<Opencc>();
        // Register ViewModels
        services.AddSingleton<MainWindowViewModel>();
        // Register MainWindow
        services.AddTransient<MainWindow>();
       
    }
}