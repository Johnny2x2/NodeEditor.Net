using System;
using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Execution;

public sealed partial class StandardNodeContext
{
    [Node("String Concat", "Strings", "Basic", "Concatenate two strings.", false)]
    public void StringConcat(string A, string B, out string Result)
    {
        ReportRunning();
        Result = string.Concat(A ?? string.Empty, B ?? string.Empty);
    }

    [Node("String Length", "Strings", "Basic", "Get length of a string.", false)]
    public void StringLength(string Input, out int Length)
    {
        ReportRunning();
        Length = (Input ?? string.Empty).Length;
    }

    [Node("Substring", "Strings", "Basic", "Get substring by start and length.", false)]
    public void StringSubstring(string Input, int Start, int Length, out string Result)
    {
        ReportRunning();
        var value = Input ?? string.Empty;
        var start = Math.Max(0, Start);
        var length = Math.Max(0, Length);

        if (start >= value.Length)
        {
            Result = string.Empty;
            return;
        }

        if (start + length > value.Length)
        {
            length = value.Length - start;
        }

        Result = value.Substring(start, length);
    }

    [Node("Replace", "Strings", "Basic", "Replace occurrences of a value.", false)]
    public void StringReplace(string Input, string OldValue, string NewValue, out string Result)
    {
        ReportRunning();
        Result = (Input ?? string.Empty).Replace(OldValue ?? string.Empty, NewValue ?? string.Empty);
    }

    [Node("To Upper", "Strings", "Basic", "Convert to upper case.", false)]
    public void StringToUpper(string Input, out string Result)
    {
        ReportRunning();
        Result = (Input ?? string.Empty).ToUpperInvariant();
    }

    [Node("To Lower", "Strings", "Basic", "Convert to lower case.", false)]
    public void StringToLower(string Input, out string Result)
    {
        ReportRunning();
        Result = (Input ?? string.Empty).ToLowerInvariant();
    }

    [Node("Trim", "Strings", "Basic", "Trim whitespace.", false)]
    public void StringTrim(string Input, out string Result)
    {
        ReportRunning();
        Result = (Input ?? string.Empty).Trim();
    }

    [Node("Contains", "Strings", "Basic", "Check if string contains value.", false)]
    public void StringContains(string Input, string Value, out bool Contains)
    {
        ReportRunning();
        Contains = (Input ?? string.Empty).Contains(Value ?? string.Empty, StringComparison.Ordinal);
    }

    [Node("Starts With", "Strings", "Basic", "Check if string starts with value.", false)]
    public void StringStartsWith(string Input, string Value, out bool StartsWith)
    {
        ReportRunning();
        StartsWith = (Input ?? string.Empty).StartsWith(Value ?? string.Empty, StringComparison.Ordinal);
    }

    [Node("Ends With", "Strings", "Basic", "Check if string ends with value.", false)]
    public void StringEndsWith(string Input, string Value, out bool EndsWith)
    {
        ReportRunning();
        EndsWith = (Input ?? string.Empty).EndsWith(Value ?? string.Empty, StringComparison.Ordinal);
    }

    [Node("Split", "Strings", "Basic", "Split string by delimiter into list.", false)]
    public void StringSplit(string Input, string Delimiter, out SerializableList List)
    {
        ReportRunning();
        List = new SerializableList();
        var value = Input ?? string.Empty;
        var split = string.IsNullOrEmpty(Delimiter)
            ? new[] { value }
            : value.Split(new[] { Delimiter }, StringSplitOptions.None);

        foreach (var item in split)
        {
            List.Add(item);
        }
    }

    [Node("Join", "Strings", "Basic", "Join list items into string.", false)]
    public void StringJoin(SerializableList List, string Delimiter, out string Result)
    {
        ReportRunning();
        var items = List?.Snapshot() ?? Array.Empty<object>();
        var parts = new string[items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            parts[i] = items[i]?.ToString() ?? string.Empty;
        }

        Result = string.Join(Delimiter ?? string.Empty, parts);
    }
}
