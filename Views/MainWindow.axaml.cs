using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using AvaloniaEdit;
using OpenccNetLibGui.ViewModels;

namespace OpenccNetLibGui.Views;

public partial class MainWindow : Window
{
    private DictionaryView? _dictionaryView;
    private SettingsView? _settingsView;

    public MainWindow()
    {
        InitializeComponent();

        var tbSource = this.FindControl<TextEditor>("TbSource");
        var tbDestination = this.FindControl<TextEditor>("TbDestination");
        var tbPreview = this.FindControl<TextEditor>("TbPreview");

        ConfigureEditor(tbSource);
        ConfigureEditor(tbDestination);
        ConfigureEditor(tbPreview);

        AddHandler(KeyDownEvent, OnMainWindowKeyDown, RoutingStrategies.Tunnel);

        var lbxSource = this.FindControl<ListBox>("LbxSource");
        InitializeDragAndDrop(tbSource);
        InitializeDragAndDrop(lbxSource);
        Closing += MainWindow_Closing;
    }

    public MainWindow(MainWindowViewModel vm) : this()
    {
        DataContext = vm;

        vm.RequestGoToSuspiciousLine += OnRequestGoToSuspiciousLine;
    }

    private void MainTabs_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TabDictionary?.IsSelected == true && _dictionaryView is null)
        {
            _dictionaryView = new DictionaryView();
            TabDictionary.Content = _dictionaryView;
        }

        if (TabSettings?.IsSelected == true && _settingsView is null)
        {
            _settingsView = new SettingsView();
            TabSettings.Content = _settingsView;
        }
    }

    private void OnRequestGoToSuspiciousLine(bool isSource, int lineNumber)
    {
        var editor = this.FindControl<TextEditor>(
            isSource ? "TbSource" : "TbDestination");

        if (editor == null)
            return;

        GoToLine(editor, lineNumber);
    }

    private static void ConfigureEditor(TextEditor? editor)
    {
        if (editor == null) return;

        editor.TextArea.TextView.Margin = new Avalonia.Thickness(3, 0, 18, 0);
    }

    private void InitializeDragAndDrop(Control? control)
    {
        if (control == null) return;
        DragDrop.SetAllowDrop(control, true);
        control.AddHandler(DragDrop.DragEnterEvent, OnDragEnter!);
        control.AddHandler(DragDrop.DragOverEvent, OnDragOver!);
        control.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private static void OnDragEnter(object sender, DragEventArgs e)
    {
        e.DragEffects = GetDragEffects(sender, e.DataTransfer);
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        e.DragEffects = GetDragEffects(sender, e.DataTransfer);
    }

    private static DragDropEffects GetDragEffects(object sender, IDataTransfer data)
    {
        if (OperatingSystem.IsLinux())
        {
            // Linux file drops often use "text/uri-list"
            if (data.Contains(DataFormat.File))
                return DragDropEffects.Copy;
        }
        else
        {
            // Windows and macOS standard behavior
            if (data.Contains(DataFormat.File))
                return DragDropEffects.Copy;
        }

        return sender switch
        {
            ListBox => data.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None,
            TextEditor => data.Contains(DataFormat.File) || data.Contains(DataFormat.Text)
                ? DragDropEffects.Copy
                : DragDropEffects.None,
            _ => DragDropEffects.None
        };
    }

    private async Task OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            var files = e.DataTransfer.Contains(DataFormat.File)
                ? e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().ToList()
                : null;

            if (files is { Count: > 0 })
            {
                await HandleFileDropAsync(sender, vm, files);
            }
        }
    }

    private static async Task HandleFileDropAsync(object sender, MainWindowViewModel vm,
        IEnumerable<IStorageFile> files)
    {
        var fileList = files.ToList(); // Materialize to avoid multiple enumerations
        if (fileList.Count == 0) return;
        string? filePath;

        switch (sender)
        {
            case TextEditor:
            {
                var firstFile = fileList[0];
                filePath = NormalizeFilePath(firstFile);
                if (filePath == null) return;

                try
                {
                    await vm.OpenPathAsync(filePath);
                }
                catch (Exception ex)
                {
                    vm.LblStatusBarContent = $"Error opening file: {ex.Message}";
                }

                break;
            }

            case ListBox:
            {
                // 1) Collect existing items (keep order)
                var items = vm.LbxSourceItems?.ToList() ?? new List<string>();
                var beforeCount = items.Count;

                // Fast de-dupe check (case-insensitive)
                var seen = new HashSet<string>(items, StringComparer.OrdinalIgnoreCase);

                // 2) Add dropped items
                foreach (var file in fileList)
                {
                    filePath = NormalizeFilePath(file);
                    if (string.IsNullOrWhiteSpace(filePath))
                        continue;

                    if (seen.Add(filePath))
                        items.Add(filePath);
                }

                // 3) Partition: non-PDF (to be sorted) + PDF (kept at bottom, preserve relative order)
                static bool IsPdf(string p) =>
                    p.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

                var pdfList = new List<string>();
                var nonPdfList = new List<string>();

                foreach (var p in items)
                {
                    if (IsPdf(p)) pdfList.Add(p);
                    else nonPdfList.Add(p);
                }

                // 4) Sort list
                nonPdfList.Sort(StringComparer.OrdinalIgnoreCase);
                pdfList.Sort(StringComparer.OrdinalIgnoreCase);

                // 5) Bulk update ObservableCollection
                vm.LbxSourceItems!.Clear();
                foreach (var p in nonPdfList) vm.LbxSourceItems.Add(p);
                foreach (var p in pdfList) vm.LbxSourceItems.Add(p);

                var afterCount = nonPdfList.Count + pdfList.Count;
                vm.LblStatusBarContent = $"File(s) dropped: {afterCount - beforeCount}";
                break;
            }
        }
    }

    // Converts Linux `file://` URIs to local paths
    private static string? NormalizeFilePath(IStorageFile file)
    {
        var filePath = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(filePath)) return null;

        if (!OperatingSystem.IsLinux() || !filePath.StartsWith("file://")) return filePath;
        filePath = filePath.Substring(7); // Remove "file://"
        filePath = Uri.UnescapeDataString(filePath); // Decode URI

        return filePath;
    }

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.PersistOnExit(
            WindowState == WindowState.Normal ? Bounds.Width : null,
            WindowState == WindowState.Normal ? Bounds.Height : null);
    }

    private void TbSource_TextChanged(object? sender, EventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel) viewModel.TbSourceTextChanged();
    }

    private void BtnExit_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    // Key-Bindings for TbSource

    private async void OnMainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            if (e.Key != Key.G || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
                return;

            e.Handled = true;
            
            var mainTab = this.FindControl<TabItem>("TabMain");
            if (mainTab?.IsSelected != true)
                return;

            var editor = this.FindControl<TextEditor>("TbSource");
            if (editor?.Document == null)
                return;

            var currentLine = editor.TextArea.Caret.Line;
            var lineNumber = await ShowGoToLineDialogAsync(
                editor.Document.LineCount,
                currentLine);

            if (lineNumber is >= 1)
                GoToLine(editor, lineNumber.Value);
        }
        catch (Exception ex)
        {
            await MessageBox.Show(
                "Go to line failed:\n" + ex.Message,
                "Error",
                this);
        }
    }

    private async Task<int?> ShowGoToLineDialogAsync(int maxLine, int currentLine)
    {
        currentLine = Math.Clamp(currentLine, 1, maxLine);

        var input = new TextBox
        {
            Text = currentLine.ToString(),
            Width = 180,
            MaxLength = maxLine.ToString().Length,
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        input.Classes.Add("go-to-line");

        var validationMessage = new TextBlock
        {
            MinHeight = 20,
            Foreground = Avalonia.Media.Brushes.IndianRed
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            IsCancel = true,
            MinWidth = 80,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

        var goButton = new Button
        {
            Content = "Go",
            IsDefault = true,
            MinWidth = 80,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

        bool TryGetValidLine(out int lineNumber)
        {
            var valid =
                int.TryParse(input.Text, out lineNumber) &&
                lineNumber >= 1 &&
                lineNumber <= maxLine;

            input.Classes.Set("invalid", !valid);
            goButton.IsEnabled = valid;

            validationMessage.Text = valid
                ? string.Empty
                : $"Enter a line number from 1 to {maxLine}.";

            return valid;
        }

        var filteringText = false;

        input.TextChanged += (_, _) =>
        {
            if (filteringText)
                return;

            var text = input.Text ?? string.Empty;
            var digitsOnly = new string(text.Where(char.IsAsciiDigit).ToArray());

            if (digitsOnly != text)
            {
                filteringText = true;
                input.Text = digitsOnly;
                input.CaretIndex = digitsOnly.Length;
                filteringText = false;
            }

            TryGetValidLine(out _);
        };

        var dialog = new Window
        {
            Title = "Go to Line",
            Width = 300,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(16),
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = $"Line number 1 - {maxLine}:" },
                    input,
                    validationMessage,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children =
                        {
                            cancelButton,
                            goButton
                        }
                    }
                }
            }
        };

        dialog.Classes.Add("dialog-surface");

        dialog.Opened += (_, _) =>
        {
            input.Focus();
            input.SelectAll();
            TryGetValidLine(out _);
        };

        dialog.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                dialog.Close(null);
            }
            else if (e.Key == Key.Enter &&
                     TryGetValidLine(out var lineNumber))
            {
                dialog.Close(lineNumber);
            }
        };

        cancelButton.Click += (_, _) => dialog.Close(null);

        goButton.Click += (_, _) =>
        {
            if (TryGetValidLine(out var lineNumber))
                dialog.Close(lineNumber);
        };

        return await dialog.ShowDialog<int?>(this);
    }

    private static void GoToLine(TextEditor editor, int lineNumber)
    {
        if (editor.Document == null)
            return;

        if (lineNumber < 1 || lineNumber > editor.Document.LineCount)
            return;

        var line = editor.Document.GetLineByNumber(lineNumber);

        var offset = line.Offset;

        editor.Focus();
        editor.TextArea.Focus();

        editor.CaretOffset = offset;
        // editor.TextArea.Caret.Offset = offset;

        var visualLine = Math.Max(1, lineNumber - 3);
        editor.ScrollToLine(visualLine);

        editor.TextArea.Caret.BringCaretToView();
    }
}