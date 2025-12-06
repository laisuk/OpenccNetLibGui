using System;
using Avalonia;
using Avalonia.Data;
using Avalonia.Xaml.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.Document;
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

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject != null)
        {
            AssociatedObject.TextArea.SelectionChanged += OnSelectionChanged;
            PropertyChanged += OnViewModelPropertyChanged;
        }
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

        var sel = editor.TextArea.Selection;
        if (sel == null || sel.IsEmpty)
        {
            _isUpdatingFromEditor = true;
            SelectionStart = 0;
            SelectionLength = 0;
            _isUpdatingFromEditor = false;
            return;
        }

        // ✅ 用 SurroundingSegment，方向無關
        var segment = sel.SurroundingSegment;
        var startOffset = segment.Offset;
        var length = segment.Length;

        _isUpdatingFromEditor = true;
        SelectionStart = startOffset;
        SelectionLength = length;
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

        if (e.Property != SelectionStartProperty && e.Property != SelectionLengthProperty)
            return;

        // 呢次 PropertyChanged 係因為 editor 自己 SetValue 觸發 → 唔好再反寫返去 editor
        if (_isUpdatingFromEditor)
            return;

        var doc = editor.Document;
        var start = SelectionStart;
        var length = SelectionLength;
        var end = start + length;

        if (start < 0 || length < 0 || end > doc.TextLength)
            return;

        var area = editor.TextArea;

        _isUpdatingFromViewModel = true;

        if (length == 0)
        {
            // 清除選區，只移動 caret
            area.ClearSelection();
            area.Caret.Offset = start;
        }
        else
        {
            // 正常設置 forward selection
            area.Selection = Selection.Create(area, start, end);
            area.Caret.Offset = end;
        }

        _isUpdatingFromViewModel = false;
    }
}