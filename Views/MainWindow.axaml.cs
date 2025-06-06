﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AvaloniaEdit;
using OpenccNetLib;
using OpenccNetLibGui.ViewModels;

namespace OpenccNetLibGui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();

        var tbSource = this.FindControl<TextEditor>("TbSource");
        var lbxSource = this.FindControl<ListBox>("LbxSource");
        InitializeDragAndDrop(tbSource);
        InitializeDragAndDrop(lbxSource);
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
        e.DragEffects = GetDragEffects(sender, e.Data);
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        e.DragEffects = GetDragEffects(sender, e.Data);
    }

    private static DragDropEffects GetDragEffects(object sender, IDataObject data)
    {
        if (OperatingSystem.IsLinux())
        {
            // Linux file drops often use "text/uri-list"
            if (data.Contains(DataFormats.Files) || data.Contains("text/uri-list"))
                return DragDropEffects.Copy;
        }
        else
        {
            // Windows and macOS standard behavior
            if (data.Contains(DataFormats.Files))
                return DragDropEffects.Copy;
        }

        return sender switch
        {
            ListBox => data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None,
            TextEditor => data.Contains(DataFormats.Files) || data.Contains(DataFormats.Text)
                ? DragDropEffects.Copy
                : DragDropEffects.None,
            _ => DragDropEffects.None
        };
    }

    private async Task OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            var files = e.Data.Contains(DataFormats.Files) ? e.Data.GetFiles()?.OfType<IStorageFile>().ToList() : null;
            IStorageFile? filePath = null;

            if (files is { Count: > 0 })
            {
                await HandleFileDropAsync(sender, vm, files);
                filePath = files.FirstOrDefault()!;
            }
            
            vm.LblStatusBarContent = $"Contents dropped {filePath?.TryGetLocalPath()}";
            var codeText = Opencc.ZhoCheck(vm.TbSourceTextDocument!.Text);
            vm.UpdateEncodeInfo(codeText);
            vm.LblFileNameContent = filePath?.Name;
            // vm.CurrentOpenFileName = filePath?.TryGetLocalPath();
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
                var firstFile = fileList[0]; // Safe access as fileList is checked
                filePath = NormalizeFilePath(firstFile);
                if (filePath == null) return;
                var content = await File.ReadAllTextAsync(filePath);
                vm.TbSourceTextDocument!.Text = content;
                break;

            case ListBox:
                var newItems = new HashSet<string>(vm.LbxSourceItems!); // Ensure uniqueness
                foreach (var file in fileList)
                {
                    filePath = NormalizeFilePath(file);
                    if (!string.IsNullOrEmpty(filePath))
                        newItems.Add(filePath);
                }

                // Clear & update ObservableCollection in bulk to minimize UI updates
                vm.LbxSourceItems!.Clear();
                foreach (var item in newItems.OrderBy(item => item))
                    vm.LbxSourceItems.Add(item);

                break;
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