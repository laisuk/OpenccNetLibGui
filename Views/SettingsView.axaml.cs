using System.Diagnostics;
using Avalonia.Controls;

namespace OpenccNetLibGui.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Debug.WriteLine("SettingsView initialized");
    }
}
