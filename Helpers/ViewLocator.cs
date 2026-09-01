using Avalonia.Controls;
using Avalonia.Controls.Templates;
using OpenccNetLibGui.ViewModels;
using OpenccNetLibGui.Views;

namespace OpenccNetLibGui.Helpers;

/// <summary>
/// Resolves view models to their corresponding Avalonia views.
///
/// View mappings are explicit so that all referenced view types remain visible
/// to the linker and NativeAOT compiler. No runtime type-name lookup or
/// reflection-based construction is used.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    /// <inheritdoc />
    public Control? Build(object? data)
    {
        if (data is null)
            return null;

        Control? control = data switch
        {
            MainWindowViewModel => new MainWindow(),
            SettingsViewModel => new SettingsView(),
            DictionaryGeneratorViewModel => new DictionaryView(),
            AboutViewModel => new AboutDialog(),
            ShortHeadingDialogViewModel => new ShortHeadingDialog(),
            _ => null,
        };

        if (control is null)
        {
            return new TextBlock
            {
                Text = $"View not found for {data.GetType().Name}",
            };
        }

        control.DataContext = data;
        return control;
    }

    /// <inheritdoc />
    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}