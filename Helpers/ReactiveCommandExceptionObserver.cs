using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;

namespace OpenccNetLibGui.Helpers;

public readonly record struct CommandExceptionInfo(string CommandName, Exception Exception);

internal static class ReactiveCommandExceptionObserver
{
    public static IDisposable Subscribe(
        Action<CommandExceptionInfo> handler,
        params (string CommandName, IObservable<Exception> Exceptions)[] commands)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(commands);

        if (commands.Length == 0)
            throw new ArgumentException("At least one command is required.", nameof(commands));

        return commands
            .Select(command => command.Exceptions.Select(exception =>
                new CommandExceptionInfo(command.CommandName, exception)))
            .Merge()
            .Subscribe(failure =>
            {
                Trace.TraceError(
                    "Reactive command {0} failed:{1}{2}",
                    failure.CommandName,
                    Environment.NewLine,
                    failure.Exception);

                if (Debugger.IsAttached)
                    Debugger.Break();

                handler(failure);
            });
    }
}
