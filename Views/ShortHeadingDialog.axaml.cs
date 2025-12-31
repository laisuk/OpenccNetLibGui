using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using OpenccNetLibGui.Services;
using OpenccNetLibGui.ViewModels;

namespace OpenccNetLibGui.Views;

public partial class ShortHeadingDialog : Window
{
    private const int MinValue = 3;
    private const int MaxValue = 30;

    // Required by Avalonia XAML loader (must be public)
    public ShortHeadingDialog()
    {
        InitializeComponent();
    }

    // Main ctor (single source of truth)
    public ShortHeadingDialog(ShortHeadingSettings? current) : this()
    {
        DataContext = new ShortHeadingDialogViewModel(current ?? ShortHeadingSettings.Default);
    }

    private ShortHeadingDialogViewModel Vm => (ShortHeadingDialogViewModel)DataContext!;

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        var s = Vm.ToSettings();
        s.MaxLen = Math.Clamp(s.MaxLen, MinValue, MaxValue);
        Close(s);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);

    private void RestoreDefault_Click(object? sender, PointerPressedEventArgs e)
    {
        Vm.LoadFrom(ShortHeadingSettings.Default);
    }
}