namespace NodeEditor.Blazor.Models;

public sealed record class NodeImage(
    string DataUrl,
    int? Width = null,
    int? Height = null
);
