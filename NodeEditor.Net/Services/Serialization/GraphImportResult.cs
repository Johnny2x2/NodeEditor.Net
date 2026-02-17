namespace NodeEditor.Net.Services.Serialization;

public sealed record GraphImportResult(IReadOnlyList<string> Warnings)
{
    public static GraphImportResult Empty { get; } = new(Array.Empty<string>());
}
