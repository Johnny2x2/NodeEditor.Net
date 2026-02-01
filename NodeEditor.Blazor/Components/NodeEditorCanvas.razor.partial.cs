using Microsoft.JSInterop;
using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Components;

public partial class NodeEditorCanvas
{
    private IJSObjectReference? _canvasJsModule;
    private DotNetObjectReference<NodeEditorCanvas>? _dotNetRef;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _dotNetRef = DotNetObjectReference.Create(this);
        _canvasJsModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            "./_content/NodeEditor.Blazor/nodeEditorCanvas.js");

        await _canvasJsModule.InvokeVoidAsync("observeCanvasSize", _canvasRef, _dotNetRef);
        await UpdateCanvasScreenOffsetAsync();
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
}