using System;
using Avalonia;
using Avalonia.Data;
using Avalonia.Xaml.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.Editing;

namespace OpenccNetLibGui.Helpers;

/// <summary>
/// Bridges AvaloniaEdit.TextEditor selection to ViewModel integer offsets
/// (SelectionStart / SelectionLength) via attached behavior.
/// </summary>
public class TextEditorSelectionBehavior : Behavior<TextEditor>
{
    private bool _isUpdatingFromEditor;
    private bool _isUpdatingFromViewModel;

    public static readonly StyledProperty<int> SelectionStartProperty =
        AvaloniaProperty.Register<TextEditorSelectionBehavior, int>(
            nameof(SelectionStart),
            defaultBindingMode: BindingMode.TwoWay);

    public int SelectionStart
    {
        get => GetValue(SelectionStartProperty);
        set => SetValue(SelectionStartProperty, value);
    }

    public static readonly StyledProperty<int> SelectionLengthProperty =
        AvaloniaProperty.Register<TextEditorSelectionBehavior, int>(
            nameof(SelectionLength),
            defaultBindingMode: BindingMode.TwoWay);

    public int SelectionLength
    {
        get => GetValue(SelectionLengthProperty);
        set => SetValue(SelectionLengthProperty, value);
    }

    // ✅ NEW: caret offset (captures forward/backward selection correctly)
    public static readonly StyledProperty<int> CaretOffsetProperty =
        AvaloniaProperty.Register<TextEditorSelectionBehavior, int>(
            nameof(CaretOffset),
            defaultBindingMode: BindingMode.TwoWay);

    public int CaretOffset
    {
        get => GetValue(CaretOffsetProperty);
        set => SetValue(CaretOffsetProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject == null) return;
        AssociatedObject.TextArea.SelectionChanged += OnSelectionChanged;
        PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.TextArea.SelectionChanged -= OnSelectionChanged;
            PropertyChanged -= OnViewModelPropertyChanged;
        }

        base.OnDetaching();
    }

    /// <summary>
    /// Sync editor → ViewModel (user changed selection in the editor).
    /// </summary>
    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        var editor = AssociatedObject;
        if (editor?.Document is null)
            return;

        if (_isUpdatingFromViewModel)
            return;

        var area = editor.TextArea;

        _isUpdatingFromEditor = true;

        // ✅ Always sync caret (works for backward selection too)
        CaretOffset = area.Caret.Offset;

        var sel = area.Selection;
        if (sel == null || sel.IsEmpty)
        {
            SelectionStart = 0;
            SelectionLength = 0;
            _isUpdatingFromEditor = false;
            return;
        }

        // Normalized segment for selection range (direction-free)
        var seg = sel.SurroundingSegment;
        SelectionStart = seg.Offset;
        SelectionLength = seg.Length;

        _isUpdatingFromEditor = false;
    }

    /// <summary>
    /// Sync ViewModel → editor (VM changed SelectionStart / SelectionLength).
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        var editor = AssociatedObject;
        if (editor?.Document is null)
            return;

        if (e.Property != SelectionStartProperty &&
            e.Property != SelectionLengthProperty &&
            e.Property != CaretOffsetProperty)
            return;

        if (_isUpdatingFromEditor)
            return;

        var doc = editor.Document;
        var area = editor.TextArea;

        var start = SelectionStart;
        var length = SelectionLength;
        var end = start + length;

        if (start < 0 || length < 0 || end > doc.TextLength)
            return;

        _isUpdatingFromViewModel = true;

        if (length == 0)
        {
            // ✅ Clear selection, restore caret using CaretOffset (not SelectionStart)
            area.ClearSelection();

            var caret = CaretOffset;
            if (caret < 0) caret = 0;
            if (caret > doc.TextLength) caret = doc.TextLength;

            area.Caret.Offset = caret;
        }
        else
        {
            area.Selection = Selection.Create(area, start, end);

            // (optional) keep caret where VM says
            var caret = CaretOffset;
            if (caret < 0) caret = start;
            if (caret > doc.TextLength) caret = end;
            area.Caret.Offset = caret;
        }

        _isUpdatingFromViewModel = false;
    }
}