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

    private bool _syncingAsciiState;

    // ✅ REQUIRED for Design.DataContext
    public ShortHeadingDialogViewModel()
        : this(ShortHeadingSettings.Default)
    {
    }

    public ShortHeadingDialogViewModel(ShortHeadingSettings s) => LoadFrom(s);

    public int MaxLen
    {
        get => _maxLen;
        set
        {
            var v = Math.Clamp(value, MinValue, MaxValue);
            this.RaiseAndSetIfChanged(ref _maxLen, v);
            this.RaisePropertyChanged(nameof(MaxLenValue));
        }
    }

    /// <summary>
    /// Bridge for NumericUpDown.Value (decimal?)
    /// </summary>
    public decimal? MaxLenValue
    {
        get => MaxLen; // int -> decimal? (implicit OK)
        set => MaxLen = (int)Math.Round((double)(value ?? MinValue));
    }

    public bool AllCjk
    {
        get => _allCjk;
        set => this.RaiseAndSetIfChanged(ref _allCjk, value);
    }

    /// <summary>
    /// Tri-state parent for ASCII selection (VS-style):
    /// true  = AllAscii enabled
    /// null  = partial (digits only)
    /// false = none
    /// </summary>
    public bool? AsciiState
    {
        get
        {
            if (AllAscii) return true;
            if (AllAsciiDigits) return null;
            return false;
        }
        set
        {
            if (_syncingAsciiState) return;

            _syncingAsciiState = true;
            try
            {
                // clicking an indeterminate checkbox typically toggles to checked
                if (value is true or null)
                {
                    AllAscii = true; // full ASCII enabled
                    // ✅ Parent checked => select all children
                    AllAsciiDigits = true;
                }
                else
                {
                    // value == false
                    AllAscii = false;
                    AllAsciiDigits = false;
                }
            }
            finally
            {
                _syncingAsciiState = false;
                this.RaisePropertyChanged();
            }
        }
    }

    public bool AllAscii
    {
        get => _allAscii;
        set
        {
            this.RaiseAndSetIfChanged(ref _allAscii, value);

            // If user turns off ASCII, digits-only cannot remain enabled.
            if (!value && AllAsciiDigits)
                AllAsciiDigits = false;

            RaiseAsciiStateChanged();
        }
    }

    public bool AllAsciiDigits
    {
        get => _allAsciiDigits;
        set
        {
            this.RaiseAndSetIfChanged(ref _allAsciiDigits, value);

            // If user selects digits-only, parent becomes indeterminate (not auto-check).
            // But if you prefer "digits implies ascii", uncomment:
            // if (value && !AllAscii) AllAscii = true;
            if (!value && AllAscii) AllAscii = false;

            RaiseAsciiStateChanged();
        }
    }

    public bool MixedCjkAscii
    {
        get => _mixedCjkAscii;
        set => this.RaiseAndSetIfChanged(ref _mixedCjkAscii, value);
    }

    private void RaiseAsciiStateChanged()
    {
        if (_syncingAsciiState) return;
        this.RaisePropertyChanged(nameof(AsciiState));
    }

    public void LoadFrom(ShortHeadingSettings s)
    {
        MaxLen = s.MaxLen;
        AllCjk = s.AllCjk;
        _allAscii = s.AllAscii;
        _allAsciiDigits = s.AllAsciiDigits;
        this.RaisePropertyChanged(nameof(AllAscii));
        this.RaisePropertyChanged(nameof(AllAsciiDigits));
        this.RaisePropertyChanged(nameof(AsciiState));
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