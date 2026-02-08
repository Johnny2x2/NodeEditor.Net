using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Plugins.LLMTornado.Contexts;

public sealed class UtilityContext : INodeContext
{
    [Node("Format Prompt",
        category: "LLM/Utilities",
        description: "Template string substitution for prompts using {key} placeholders",
        isCallable: false)]
    public void FormatPrompt(
        string Template,
        string? Key1,
        string? Value1,
        string? Key2,
        string? Value2,
        string? Key3,
        string? Value3,
        out string FormattedPrompt)
    {
        var result = Template ?? string.Empty;

        if (!string.IsNullOrEmpty(Key1))
            result = result.Replace($"{{{Key1}}}", Value1 ?? string.Empty);
        if (!string.IsNullOrEmpty(Key2))
            result = result.Replace($"{{{Key2}}}", Value2 ?? string.Empty);
        if (!string.IsNullOrEmpty(Key3))
            result = result.Replace($"{{{Key3}}}", Value3 ?? string.Empty);

        FormattedPrompt = result;
    }

    [Node("Token Counter",
        category: "LLM/Utilities",
        description: "Estimate token count for text (approximate: ~1.3 tokens per word)",
        isCallable: false)]
    public void CountTokens(
        string Text,
        out int EstimatedTokens)
    {
        if (string.IsNullOrEmpty(Text))
        {
            EstimatedTokens = 0;
            return;
        }

        var wordCount = Text.Split(
            new[] { ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries).Length;
        EstimatedTokens = (int)(wordCount * 1.3);
    }

    [Node("Combine Strings",
        category: "LLM/Utilities",
        description: "Concatenate two strings with an optional separator",
        isCallable: false)]
    public void CombineStrings(
        string A,
        string B,
        string? Separator,
        out string Combined)
    {
        Combined = string.Join(Separator ?? string.Empty, A ?? string.Empty, B ?? string.Empty);
    }
}
