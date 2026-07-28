using ReactiveUI.Reactive;
using System;
using System.Reactive.Disposables;

namespace OpenccNetLibGui.ViewModels;

public class ViewModelBase : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _subscriptions = new();

    protected void TrackSubscription(IDisposable subscription) => _subscriptions.Add(subscription);

    public void Dispose()
    {
        _subscriptions.Dispose();
        GC.SuppressFinalize(this);
    }
}