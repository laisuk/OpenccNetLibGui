using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;

namespace OpenccNetLibGui.Helpers;

public class AutoScrollBehavior : Behavior<ListBox>
{
    private INotifyCollectionChanged? _notifyCollection;

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject?.Items is not INotifyCollectionChanged ncc) return;
        _notifyCollection = ncc;
        ncc.CollectionChanged += OnCollectionChanged;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        if (_notifyCollection == null) return;
        _notifyCollection.CollectionChanged -= OnCollectionChanged;
        _notifyCollection = null;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (AssociatedObject == null)
            return;

        // Only care about new items (or you can also allow Reset if you like)
        if (e.Action != NotifyCollectionChangedAction.Add)
            return;

        // No items? nothing to scroll to
        if (AssociatedObject.ItemCount == 0)
            return;

        // Always scroll to the actual last item in the ListBox
        var lastItem = AssociatedObject.Items[AssociatedObject.ItemCount - 1];

        // Defer until layout is updated → avoids "last 2 lines cut off"
        Dispatcher.UIThread.Post(
            () => AssociatedObject.ScrollIntoView(lastItem!),
            DispatcherPriority.Background);
    }
}

