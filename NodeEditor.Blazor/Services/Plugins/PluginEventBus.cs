using System.Collections.Concurrent;
using System.Threading;
using NodeEditor.Blazor.Services;

namespace NodeEditor.Blazor.Services.Plugins;

public sealed class PluginEventBus : IPluginEventBus, IDisposable
{
    private readonly NodeEditorState _state;
    private readonly ConcurrentBag<IDisposable> _subscriptions = new();

    public PluginEventBus(NodeEditorState state)
    {
        _state = state;
        HookStateEvents();
    }

    public IDisposable SubscribeNodeAdded(Action<NodeEventArgs> handler) => AddHandler(_nodeAddedHandlers, handler);

    public IDisposable SubscribeNodeRemoved(Action<NodeEventArgs> handler) => AddHandler(_nodeRemovedHandlers, handler);

    public IDisposable SubscribeConnectionAdded(Action<ConnectionEventArgs> handler) => AddHandler(_connectionAddedHandlers, handler);

    public IDisposable SubscribeConnectionRemoved(Action<ConnectionEventArgs> handler) => AddHandler(_connectionRemovedHandlers, handler);

    public IDisposable SubscribeSelectionChanged(Action<SelectionChangedEventArgs> handler) => AddHandler(_selectionChangedHandlers, handler);

    public IDisposable SubscribeConnectionSelectionChanged(Action<ConnectionSelectionChangedEventArgs> handler) => AddHandler(_connectionSelectionChangedHandlers, handler);

    public IDisposable SubscribeViewportChanged(Action<ViewportChangedEventArgs> handler) => AddHandler(_viewportChangedHandlers, handler);

    public IDisposable SubscribeZoomChanged(Action<ZoomChangedEventArgs> handler) => AddHandler(_zoomChangedHandlers, handler);

    public IDisposable SubscribeSocketValuesChanged(Action handler) => AddHandler(_socketValuesChangedHandlers, handler);

    public IDisposable SubscribeNodeExecutionStateChanged(Action<NodeEventArgs> handler) => AddHandler(_nodeExecutionStateChangedHandlers, handler);

    public void PublishNodeAdded(NodeEventArgs args) => Publish(_nodeAddedHandlers, args);

    public void PublishNodeRemoved(NodeEventArgs args) => Publish(_nodeRemovedHandlers, args);

    public void PublishConnectionAdded(ConnectionEventArgs args) => Publish(_connectionAddedHandlers, args);

    public void PublishConnectionRemoved(ConnectionEventArgs args) => Publish(_connectionRemovedHandlers, args);

    public void PublishSelectionChanged(SelectionChangedEventArgs args) => Publish(_selectionChangedHandlers, args);

    public void PublishConnectionSelectionChanged(ConnectionSelectionChangedEventArgs args) => Publish(_connectionSelectionChangedHandlers, args);

    public void PublishViewportChanged(ViewportChangedEventArgs args) => Publish(_viewportChangedHandlers, args);

    public void PublishZoomChanged(ZoomChangedEventArgs args) => Publish(_zoomChangedHandlers, args);

    public void PublishSocketValuesChanged() => Publish(_socketValuesChangedHandlers);

    public void PublishNodeExecutionStateChanged(NodeEventArgs args) => Publish(_nodeExecutionStateChangedHandlers, args);

    public void Dispose()
    {
        UnhookStateEvents();
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
    }

    private readonly HandlerCollection<NodeEventArgs> _nodeAddedHandlers = new();
    private readonly HandlerCollection<NodeEventArgs> _nodeRemovedHandlers = new();
    private readonly HandlerCollection<ConnectionEventArgs> _connectionAddedHandlers = new();
    private readonly HandlerCollection<ConnectionEventArgs> _connectionRemovedHandlers = new();
    private readonly HandlerCollection<SelectionChangedEventArgs> _selectionChangedHandlers = new();
    private readonly HandlerCollection<ConnectionSelectionChangedEventArgs> _connectionSelectionChangedHandlers = new();
    private readonly HandlerCollection<ViewportChangedEventArgs> _viewportChangedHandlers = new();
    private readonly HandlerCollection<ZoomChangedEventArgs> _zoomChangedHandlers = new();
    private readonly HandlerCollection _socketValuesChangedHandlers = new();
    private readonly HandlerCollection<NodeEventArgs> _nodeExecutionStateChangedHandlers = new();

    private void HookStateEvents()
    {
        _nodeAddedHandler = (_, args) => PublishNodeAdded(args);
        _nodeRemovedHandler = (_, args) => PublishNodeRemoved(args);
        _connectionAddedHandler = (_, args) => PublishConnectionAdded(args);
        _connectionRemovedHandler = (_, args) => PublishConnectionRemoved(args);
        _selectionChangedHandler = (_, args) => PublishSelectionChanged(args);
        _connectionSelectionChangedHandler = (_, args) => PublishConnectionSelectionChanged(args);
        _viewportChangedHandler = (_, args) => PublishViewportChanged(args);
        _zoomChangedHandler = (_, args) => PublishZoomChanged(args);
        _socketValuesChangedHandler = (_, _) => PublishSocketValuesChanged();
        _nodeExecutionStateChangedHandler = (_, args) => PublishNodeExecutionStateChanged(args);

        _state.NodeAdded += _nodeAddedHandler;
        _state.NodeRemoved += _nodeRemovedHandler;
        _state.ConnectionAdded += _connectionAddedHandler;
        _state.ConnectionRemoved += _connectionRemovedHandler;
        _state.SelectionChanged += _selectionChangedHandler;
        _state.ConnectionSelectionChanged += _connectionSelectionChangedHandler;
        _state.ViewportChanged += _viewportChangedHandler;
        _state.ZoomChanged += _zoomChangedHandler;
        _state.SocketValuesChanged += _socketValuesChangedHandler;
        _state.NodeExecutionStateChanged += _nodeExecutionStateChangedHandler;
    }

    private void UnhookStateEvents()
    {
        if (_nodeAddedHandler is not null)
        {
            _state.NodeAdded -= _nodeAddedHandler;
            _state.NodeRemoved -= _nodeRemovedHandler;
            _state.ConnectionAdded -= _connectionAddedHandler;
            _state.ConnectionRemoved -= _connectionRemovedHandler;
            _state.SelectionChanged -= _selectionChangedHandler;
            _state.ConnectionSelectionChanged -= _connectionSelectionChangedHandler;
            _state.ViewportChanged -= _viewportChangedHandler;
            _state.ZoomChanged -= _zoomChangedHandler;
            _state.SocketValuesChanged -= _socketValuesChangedHandler;
            _state.NodeExecutionStateChanged -= _nodeExecutionStateChangedHandler;
        }
    }

    private EventHandler<NodeEventArgs>? _nodeAddedHandler;
    private EventHandler<NodeEventArgs>? _nodeRemovedHandler;
    private EventHandler<ConnectionEventArgs>? _connectionAddedHandler;
    private EventHandler<ConnectionEventArgs>? _connectionRemovedHandler;
    private EventHandler<SelectionChangedEventArgs>? _selectionChangedHandler;
    private EventHandler<ConnectionSelectionChangedEventArgs>? _connectionSelectionChangedHandler;
    private EventHandler<ViewportChangedEventArgs>? _viewportChangedHandler;
    private EventHandler<ZoomChangedEventArgs>? _zoomChangedHandler;
    private EventHandler? _socketValuesChangedHandler;
    private EventHandler<NodeEventArgs>? _nodeExecutionStateChangedHandler;

    private IDisposable AddHandler<T>(HandlerCollection<T> collection, Action<T> handler)
    {
        var subscription = collection.Add(handler);
        _subscriptions.Add(subscription);
        return subscription;
    }

    private IDisposable AddHandler(HandlerCollection collection, Action handler)
    {
        var subscription = collection.Add(handler);
        _subscriptions.Add(subscription);
        return subscription;
    }

    private static void Publish<T>(HandlerCollection<T> collection, T args)
    {
        collection.Publish(args);
    }

    private static void Publish(HandlerCollection collection)
    {
        collection.Publish();
    }

    private sealed class HandlerCollection
    {
        private readonly object _lock = new();
        private readonly List<Action> _handlers = new();

        public IDisposable Add(Action handler)
        {
            lock (_lock)
            {
                _handlers.Add(handler);
            }

            return new Subscription(() => Remove(handler));
        }

        public void Publish()
        {
            Action[] snapshot;
            lock (_lock)
            {
                snapshot = _handlers.ToArray();
            }

            foreach (var handler in snapshot)
            {
                handler();
            }
        }

        private void Remove(Action handler)
        {
            lock (_lock)
            {
                _handlers.Remove(handler);
            }
        }
    }

    private sealed class HandlerCollection<T>
    {
        private readonly object _lock = new();
        private readonly List<Action<T>> _handlers = new();

        public IDisposable Add(Action<T> handler)
        {
            lock (_lock)
            {
                _handlers.Add(handler);
            }

            return new Subscription(() => Remove(handler));
        }

        public void Publish(T args)
        {
            Action<T>[] snapshot;
            lock (_lock)
            {
                snapshot = _handlers.ToArray();
            }

            foreach (var handler in snapshot)
            {
                handler(args);
            }
        }

        private void Remove(Action<T> handler)
        {
            lock (_lock)
            {
                _handlers.Remove(handler);
            }
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _onDispose;
        private int _disposed;

        public Subscription(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _onDispose();
        }
    }
}