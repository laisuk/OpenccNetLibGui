using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace OpenccNetLibGui.Views;

public class MessageBox : Window
{
    private MessageBox(string message, string title)
    {
        Title = title;
        Width = 300;
        Height = 100;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var stackPanel = new StackPanel { Margin = new Thickness(10) };
        var textBlock = new TextBlock { Text = message, Margin = new Thickness(0, 0, 0, 20) };
        var button = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Center };

        button.Click += (_, _) => Close();

        stackPanel.Children.Add(textBlock);
        stackPanel.Children.Add(button);

        Content = stackPanel;
    }

    public static async Task Show(string message, string title, Window owner)
    {
        var messageBox = new MessageBox(message, title)
        {
            Owner = owner
        };

        await messageBox.ShowDialog(owner);
    }
}