using System.Diagnostics;
using Avalonia.Controls;

namespace OpenccNetLibGui.Views;

public partial class DictionaryView : UserControl
{
    public DictionaryView()
    {
        InitializeComponent();
        Debug.WriteLine("DictionaryView initialized");
    }
}
