using NodeEditor.Net.Services;
using NodeEditor.Net.Services.Logging;

namespace NodeEditor.Net.Services.Plugins;

public interface IPluginEventBus
{
    IDisposable SubscribeNodeAdded(Action<NodeEventArgs> handler);
    IDisposable SubscribeNodeRemoved(Action<NodeEventArgs> handler);
    IDisposable SubscribeConnectionAdded(Action<ConnectionEventArgs> handler);
    IDisposable SubscribeConnectionRemoved(Action<ConnectionEventArgs> handler);
    IDisposable SubscribeSelectionChanged(Action<SelectionChangedEventArgs> handler);
    IDisposable SubscribeConnectionSelectionChanged(Action<ConnectionSelectionChangedEventArgs> handler);
    IDisposable SubscribeViewportChanged(Action<ViewportChangedEventArgs> handler);
    IDisposable SubscribeZoomChanged(Action<ZoomChangedEventArgs> handler);
    IDisposable SubscribeSocketValuesChanged(Action handler);
    IDisposable SubscribeNodeExecutionStateChanged(Action<NodeEventArgs> handler);
    IDisposable SubscribeLogMessage(Action<LogEntry> handler);

    void PublishNodeAdded(NodeEventArgs args);
    void PublishNodeRemoved(NodeEventArgs args);
    void PublishConnectionAdded(ConnectionEventArgs args);
    void PublishConnectionRemoved(ConnectionEventArgs args);
    void PublishSelectionChanged(SelectionChangedEventArgs args);
    void PublishConnectionSelectionChanged(ConnectionSelectionChangedEventArgs args);
    void PublishViewportChanged(ViewportChangedEventArgs args);
    void PublishZoomChanged(ZoomChangedEventArgs args);
    void PublishSocketValuesChanged();
    void PublishNodeExecutionStateChanged(NodeEventArgs args);
    void PublishLogMessage(LogEntry entry);
}