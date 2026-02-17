namespace NodeEditor.Net.Services.Plugins;

public readonly record struct PluginValidationResult(bool IsValid, string? Message)
{
    public static PluginValidationResult Success() => new(true, null);

    public static PluginValidationResult Fail(string message) => new(false, message);
}
