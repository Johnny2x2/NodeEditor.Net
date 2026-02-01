using System;

namespace NodeEditor
{
    public partial class StandardNodeContext
    {
        [Node("String Concat", "Strings", "Basic", "Concatenate two strings.", false)]
        public void StringConcat(string a, string b, out string result)
        {
            ReportRunning();
            result = string.Concat(a ?? string.Empty, b ?? string.Empty);
        }

        [Node("String Length", "Strings", "Basic", "Get length of a string.", false)]
        public void StringLength(string input, out nNum length)
        {
            ReportRunning();
            length = new nNum((input ?? string.Empty).Length);
        }

        [Node("Substring", "Strings", "Basic", "Get substring by start and length.", false)]
        public void StringSubstring(string input, nNum start, nNum length, out string result)
        {
            ReportRunning();
            var value = input ?? string.Empty;
            var startIndex = Math.Max(0, start.ToInt);
            var len = Math.Max(0, length.ToInt);

            if (startIndex >= value.Length)
            {
                result = string.Empty;
                return;
            }

            if (startIndex + len > value.Length)
            {
                len = value.Length - startIndex;
            }

            result = value.Substring(startIndex, len);
        }

        [Node("Replace", "Strings", "Basic", "Replace occurrences of a value.", false)]
        public void StringReplace(string input, string oldValue, string newValue, out string result)
        {
            ReportRunning();
            result = (input ?? string.Empty).Replace(oldValue ?? string.Empty, newValue ?? string.Empty);
        }

        [Node("To Upper", "Strings", "Basic", "Convert to upper case.", false)]
        public void StringToUpper(string input, out string result)
        {
            ReportRunning();
            result = (input ?? string.Empty).ToUpperInvariant();
        }

        [Node("To Lower", "Strings", "Basic", "Convert to lower case.", false)]
        public void StringToLower(string input, out string result)
        {
            ReportRunning();
            result = (input ?? string.Empty).ToLowerInvariant();
        }

        [Node("Trim", "Strings", "Basic", "Trim whitespace.", false)]
        public void StringTrim(string input, out string result)
        {
            ReportRunning();
            result = (input ?? string.Empty).Trim();
        }

        [Node("Contains", "Strings", "Basic", "Check if string contains value.", false)]
        public void StringContains(string input, string value, out bool contains)
        {
            ReportRunning();
            contains = (input ?? string.Empty).Contains(value ?? string.Empty, StringComparison.Ordinal);
        }

        [Node("Starts With", "Strings", "Basic", "Check if string starts with value.", false)]
        public void StringStartsWith(string input, string value, out bool startsWith)
        {
            ReportRunning();
            startsWith = (input ?? string.Empty).StartsWith(value ?? string.Empty, StringComparison.Ordinal);
        }

        [Node("Ends With", "Strings", "Basic", "Check if string ends with value.", false)]
        public void StringEndsWith(string input, string value, out bool endsWith)
        {
            ReportRunning();
            endsWith = (input ?? string.Empty).EndsWith(value ?? string.Empty, StringComparison.Ordinal);
        }

        [Node("Split", "Strings", "Basic", "Split string by delimiter into list.", false)]
        public void StringSplit(string input, string delimiter, out SerializableList list)
        {
            ReportRunning();
            list = new SerializableList();
            var value = input ?? string.Empty;
            var split = string.IsNullOrEmpty(delimiter)
                ? new[] { value }
                : value.Split(new[] { delimiter }, StringSplitOptions.None);

            foreach (var item in split)
            {
                list.Add(item);
            }
        }

        [Node("Join", "Strings", "Basic", "Join list items into string.", false)]
        public void StringJoin(SerializableList list, string delimiter, out string result)
        {
            ReportRunning();
            var items = list?.Snapshot() ?? Array.Empty<object>();
            var parts = new string[items.Length];
            for (var i = 0; i < items.Length; i++)
            {
                parts[i] = items[i]?.ToString() ?? string.Empty;
            }

            result = string.Join(delimiter ?? string.Empty, parts);
        }
    }
}
