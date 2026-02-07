using System.Collections.Concurrent;

namespace NodeEditor.Blazor.Services.Execution;

/// <summary>
/// A lightweight publish-subscribe event bus scoped to a single execution run.
/// "Custom Event" nodes register handlers, and "Trigger Event" nodes fire them.
/// Thread-safe for use with parallel execution.
/// </summary>
public sealed class ExecutionEventBus
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<Func<Task>>> _handlers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a handler that will be invoked when the named event is triggered.
    /// </summary>
    public void Subscribe(string eventName, Func<Task> handler)
    {
        var bag = _handlers.GetOrAdd(eventName, _ => new ConcurrentBag<Func<Task>>());
        bag.Add(handler);
    }

    /// <summary>
    /// Triggers all handlers registered for the named event.
    /// Handlers execute concurrently; all must complete before this returns.
    /// </summary>
    public async Task TriggerAsync(string eventName, CancellationToken token = default)
    {
        if (!_handlers.TryGetValue(eventName, out var bag))
            return;

        var tasks = bag.Select(handler =>
        {
            token.ThrowIfCancellationRequested();
            return handler();
        }).ToList();

        if (tasks.Count > 0)
            await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns true if any handlers are registered for the named event.
    /// </summary>
    public bool HasSubscribers(string eventName)
    {
        return _handlers.TryGetValue(eventName, out var bag) && !bag.IsEmpty;
    }
}
