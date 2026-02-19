using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AvaloniaEdit;
using OpenccNetLibGui.ViewModels;

namespace OpenccNetLibGui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var tbSource = this.FindControl<TextEditor>("TbSource");
        var lbxSource = this.FindControl<ListBox>("LbxSource");
        InitializeDragAndDrop(tbSource);
        InitializeDragAndDrop(lbxSource);
    }

    public MainWindow(MainWindowViewModel vm) : this()
    {
        DataContext = vm;
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

    private void TbSource_TextChanged(object? sender, EventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel) viewModel.TbSourceTextChanged();
    }

    private void BtnExit_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}