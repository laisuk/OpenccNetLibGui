using System;
using Avalonia;
using Avalonia.Controls;

namespace OpenccNetLibGui.Helpers;

public static class FocusAttachedProperty
{
    public static readonly AttachedProperty<bool> IsFocusedProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>(
            "IsFocused", typeof(FocusAttachedProperty));

    static FocusAttachedProperty()
    {
        IsFocusedProperty.Changed.Subscribe(args =>
        {
            if (args is { Sender: Control control, NewValue.Value: true }) control.Focus();
        });
    }

    public static bool GetIsFocused(Control control)
    {
        return control.GetValue(IsFocusedProperty);
    }

    public static void SetIsFocused(Control control, bool value)
    {
        control.SetValue(IsFocusedProperty, value);
    }
}