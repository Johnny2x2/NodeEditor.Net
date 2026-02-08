using NodeEditor.Net.Models;
using NodeEditor.Net.ViewModels;

namespace NodeEditor.Blazor.Services.Editors;

public sealed class SocketEditorContext
{
    public required SocketViewModel Socket { get; init; }

    public required NodeViewModel Node { get; init; }

    public required Action<object?> SetValue { get; init; }

    public object? Value => Socket.Data.Value?.ToObject<object>();

    public T? GetValue<T>() where T : struct => Socket.Data.Value?.ToObject<T>();

    public string? GetStringValue() => Socket.Data.Value?.ToObject<string>();

    public void SetValueTyped<T>(T? value) => SetValue(value);
}
