using Microsoft.JSInterop;
using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Components;

public partial class NodeEditorCanvas
{
    private IJSObjectReference? _canvasJsModule;
    private DotNetObjectReference<NodeEditorCanvas>? _dotNetRef;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            _canvasJsModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                "./_content/NodeEditor.Blazor/nodeEditorCanvas.js");

            await _canvasJsModule.InvokeVoidAsync("observeCanvasSize", _canvasRef, _dotNetRef);
            await UpdateCanvasScreenOffsetAsync();
        }

        await UpdateSocketPositionsAsync();
    }

    [JSInvokable]
    public void OnCanvasResize(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var viewport = State.Viewport;
        State.Viewport = new Rect2D(viewport.X, viewport.Y, width, height);
        UpdateCulling();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();

        if (_canvasJsModule is not null)
        {
            await _canvasJsModule.InvokeVoidAsync("disconnectCanvasObserver", _canvasRef);
            await _canvasJsModule.DisposeAsync();
        }

        _dotNetRef?.Dispose();
    }

    private sealed record class SocketDotPosition(
        string NodeId,
        string SocketName,
        bool IsInput,
        double X,
        double Y);

    private async Task UpdateSocketPositionsAsync()
    {
        if (_canvasJsModule is null)
        {
            return;
        }

        var positions = await _canvasJsModule.InvokeAsync<SocketDotPosition[]>(
            "getSocketDotPositions",
            _canvasRef);

        if (positions.Length == 0)
        {
            return;
        }

        var updated = false;

        foreach (var pos in positions)
        {
            var node = State.Nodes.FirstOrDefault(n => n.Data.Id == pos.NodeId);
            if (node is null)
            {
                continue;
            }

            var sockets = pos.IsInput ? node.Inputs : node.Outputs;
            var socket = sockets.FirstOrDefault(s => s.Data.Name == pos.SocketName);
            if (socket is null)
            {
                continue;
            }

            var graphPoint = CoordinateConverter.ScreenToGraph(new Point2D(pos.X, pos.Y));
            var key = $"{pos.NodeId}:{pos.IsInput}:{pos.SocketName}";

            if (!_socketPositionCache.TryGetValue(key, out var existing) || existing != graphPoint)
            {
                _socketPositionCache[key] = graphPoint;
                socket.Position = graphPoint;
                updated = true;
            }
        }

        if (updated)
        {
            await InvokeAsync(StateHasChanged);
        }
    }
}