using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;

namespace OpenccNetLibGui.Views
{
    public partial class ShortHeadingDialog : Window
    {
        private const int DefaultValue = 8;
        private const int MinValue = 3;
        private const int MaxValue = 30;

        // 👉 Required by Avalonia (XAML loader, must be public) 
        public ShortHeadingDialog()
        {
            InitializeComponent();
        }

        // 👉 Convenience constructor for external usage
        public ShortHeadingDialog(int currentValue)
            : this()  // ensures InitializeComponent() runs once
        {
            // Clamp initial value
            var clamped = Math.Clamp(currentValue, MinValue, MaxValue);

            // Assign to NumericUpDown
            ValueBox.Value = clamped;
        }

        // 👉 OK button: return int
        private void Ok_Click(object? sender, RoutedEventArgs e)
        {
            // NumericUpDown.Value is double?, fallback to default
            var raw = ValueBox.Value ?? DefaultValue;

            var final = Math.Clamp((int)raw, MinValue, MaxValue);

            Close(final); // return int value
        }

        // 👉 Cancel button: return null
        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            Close(null); // no value
        }

        // 👉 Restore default link pressed
        private void RestoreDefault_Click(object? sender, PointerPressedEventArgs e)
        {
            ValueBox.Value = DefaultValue;
        }
    }
}