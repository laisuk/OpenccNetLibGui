using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace OpenccNetLibGui.Views;

public enum MessageBoxResult
{
    None,
    Ok,
    Cancel,
    Custom
}

public sealed class MessageBoxButton
{
    public string Text { get; }
    public MessageBoxResult Result { get; }
    public bool IsDefault { get; }
    public bool IsCancel { get; }

    public MessageBoxButton(
        string text,
        MessageBoxResult result,
        bool isDefault = false,
        bool isCancel = false)
    {
        Text = text;
        Result = result;
        IsDefault = isDefault;
        IsCancel = isCancel;
    }
}

public class MessageBox : Window
{
    private MessageBox(
        string message,
        string title,
        double width,
        double minHeight,
        IReadOnlyList<MessageBoxButton> buttons)
    {
        Title = title;
        Width = width;
        MinHeight = minHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        SizeToContent = SizeToContent.Height;

        var messageTextBox = new TextBox
        {
            Text = message,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinLines = 6,
            CaretBlinkInterval = TimeSpan.Zero,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 8,
            Margin = new Thickness(0, 16, 0, 0)
        };

        foreach (var item in buttons)
        {
            var button = new Button
            {
                Content = item.Text,
                MinWidth = 80,
                IsDefault = item.IsDefault,
                IsCancel = item.IsCancel,

                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            button.Click += (_, _) => Close(item.Result);
            buttonPanel.Children.Add(button);
        }

        Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 0,
            Children =
            {
                messageTextBox,
                buttonPanel
            }
        };
    }

    public static Task Show(
        string message,
        string title,
        Window owner,
        double width = 460,
        double minHeight = 180)
    {
        return ShowResult(
            message,
            title,
            owner,
            width,
            minHeight);
    }

    public static async Task<MessageBoxResult> ShowResult(
        string message,
        string title,
        Window owner,
        double width = 460,
        double minHeight = 180,
        params MessageBoxButton[]? buttons)
    {
        if (buttons == null || buttons.Length == 0)
        {
            buttons = new[]
            {
                new MessageBoxButton(
                    "OK",
                    MessageBoxResult.Ok,
                    isDefault: true,
                    isCancel: true)
            };
        }

        var messageBox = new MessageBox(
            message,
            title,
            width,
            minHeight,
            buttons)
        {
            Owner = owner
        };

        return await messageBox.ShowDialog<MessageBoxResult>(owner);
    }
}