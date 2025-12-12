using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using OpenccNetLibGui.Services;
using ReactiveUI;

namespace OpenccNetLibGui.Views;

public partial class ShortHeadingDialog : Window
{
    private const int MinValue = 3;
    private const int MaxValue = 30;

    // Required by Avalonia XAML loader
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

public sealed class ShortHeadingDialogViewModel : ReactiveObject
{
    private const int MinValue = 3;
    private const int MaxValue = 30;

    private int _maxLen;
    private bool _allCjk;
    private bool _allAscii;
    private bool _allAsciiDigits;
    private bool _mixedCjkAscii;

    // ✅ REQUIRED for Design.DataContext
    public ShortHeadingDialogViewModel()
        : this(ShortHeadingSettings.Default)
    {
    }
    
    public ShortHeadingDialogViewModel(ShortHeadingSettings s) => LoadFrom(s);

    /// <summary>
    /// Integer max length used by your core logic.
    /// </summary>
    public int MaxLen
    {
        get => _maxLen;
        set
        {
            var v = Math.Clamp(value, MinValue, MaxValue);
            this.RaiseAndSetIfChanged(ref _maxLen, v);
            this.RaisePropertyChanged(nameof(MaxLenValue)); // keep NumericUpDown synced
        }
    }

    /// <summary>
    /// Bridge for Avalonia NumericUpDown.Value (double? in many versions).
    /// Bind XAML to this to avoid type mismatch.
    /// </summary>
    public double MaxLenValue
    {
        get => MaxLen;
        set => MaxLen = (int)Math.Round(value);
    }

    public bool AllCjk
    {
        get => _allCjk;
        set => this.RaiseAndSetIfChanged(ref _allCjk, value);
    }

    public bool AllAscii
    {
        get => _allAscii;
        set => this.RaiseAndSetIfChanged(ref _allAscii, value);
    }

    public bool AllAsciiDigits
    {
        get => _allAsciiDigits;
        set => this.RaiseAndSetIfChanged(ref _allAsciiDigits, value);
    }

    public bool MixedCjkAscii
    {
        get => _mixedCjkAscii;
        set => this.RaiseAndSetIfChanged(ref _mixedCjkAscii, value);
    }

    public void LoadFrom(ShortHeadingSettings s)
    {
        MaxLen = s.MaxLen;
        AllCjk = s.AllCjk;
        AllAscii = s.AllAscii;
        AllAsciiDigits = s.AllAsciiDigits;
        MixedCjkAscii = s.MixedCjkAscii;
    }

    public ShortHeadingSettings ToSettings() => new()
    {
        MaxLen = MaxLen,
        AllCjk = AllCjk,
        AllAscii = AllAscii,
        AllAsciiDigits = AllAsciiDigits,
        MixedCjkAscii = MixedCjkAscii
    };
}
