using NodeEditor.Blazor;

namespace BasicNodeEditor.Contexts;

/// <summary>
/// Provides string manipulation operations as nodes.
/// </summary>
public class StringNodeContext : INodeContext
{
    [Node("Concatenate", Category = "String", Description = "Joins two strings")]
    public void Concat(
        [Socket("Text", DefaultValue = "")] string a,
        [Socket("Text", DefaultValue = "")] string b,
        [Socket("Text")] out string result)
    {
        result = a + b;
    }

    [Node("To Upper", Category = "String", Description = "Converts to uppercase")]
    public void ToUpper(
        [Socket("Text", DefaultValue = "")] string input,
        [Socket("Text")] out string result)
    {
        result = input?.ToUpper() ?? string.Empty;
    }

    [Node("To Lower", Category = "String", Description = "Converts to lowercase")]
    public void ToLower(
        [Socket("Text", DefaultValue = "")] string input,
        [Socket("Text")] out string result)
    {
        result = input?.ToLower() ?? string.Empty;
    }

    [Node("Length", Category = "String", Description = "Gets string length")]
    public void Length(
        [Socket("Text", DefaultValue = "")] string input,
        [Socket("Number")] out int result)
    {
        result = input?.Length ?? 0;
    }

    [Node("Contains", Category = "String", Description = "Checks if string contains substring")]
    public void Contains(
        [Socket("Text", DefaultValue = "")] string input,
        [Socket("Text", DefaultValue = "")] string search,
        [Socket("Boolean")] out bool result)
    {
        result = input?.Contains(search) ?? false;
    }

    [Node("Replace", Category = "String", Description = "Replaces old value with new value")]
    public void Replace(
        [Socket("Text", DefaultValue = "")] string input,
        [Socket("Text", DefaultValue = "")] string oldValue,
        [Socket("Text", DefaultValue = "")] string newValue,
        [Socket("Text")] out string result)
    {
        result = input?.Replace(oldValue, newValue) ?? string.Empty;
    }

    [Node("Substring", Category = "String", Description = "Extracts substring")]
    public void Substring(
        [Socket("Text", DefaultValue = "")] string input,
        [Socket("Number", DefaultValue = 0)] int start,
        [Socket("Number", DefaultValue = 1)] int length,
        [Socket("Text")] out string result)
    {
        if (string.IsNullOrEmpty(input) || start < 0 || start >= input.Length)
        {
            result = string.Empty;
            return;
        }

        var actualLength = Math.Min(length, input.Length - start);
        result = input.Substring(start, actualLength);
    }

    [Node("Number to String", Category = "String", Description = "Converts number to string")]
    public void NumberToString(
        [Socket("Number", DefaultValue = 0)] double number,
        [Socket("Text")] out string result)
    {
        result = number.ToString();
    }

    [Node("String to Number", Category = "String", Description = "Converts string to number")]
    public void StringToNumber(
        [Socket("Text", DefaultValue = "0")] string input,
        [Socket("Number")] out double result)
    {
        result = double.TryParse(input, out var value) ? value : 0;
    }
}
