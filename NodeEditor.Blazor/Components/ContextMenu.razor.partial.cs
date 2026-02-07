using Microsoft.JSInterop;

namespace NodeEditor.Blazor.Components;

public partial class ContextMenu
{
    private IJSObjectReference? _contextMenuJsModule;

    private async Task UpdateMenuPositionAsync()
    {
        if (!IsOpen)
        {
            return;
        }

        _contextMenuJsModule ??= await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            "./_content/NodeEditor.Blazor/contextMenu.js");

        if (_contextMenuJsModule is null)
        {
            return;
        }

        await _contextMenuJsModule.InvokeVoidAsync(
            "positionContextMenu",
            _menuRef,
            Position.X,
            Position.Y,
            12);
    }

    public async ValueTask DisposeAsync()
    {
        if (_contextMenuJsModule is not null)
        {
            try
            {
                await _contextMenuJsModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Ignored
            }
            catch (TaskCanceledException)
            {
                // Ignored
            }
        }
    }
}
