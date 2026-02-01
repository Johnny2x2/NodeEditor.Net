using NodeEditor.Blazor;

namespace BasicNodeEditor.Contexts;

/// <summary>
/// Provides logical operations as nodes.
/// </summary>
public class LogicNodeContext : INodeContext
{
    [Node("AND", Category = "Logic", Description = "Logical AND operation")]
    public void And(
        [Socket("Boolean", DefaultValue = false)] bool a,
        [Socket("Boolean", DefaultValue = false)] bool b,
        [Socket("Boolean")] out bool result)
    {
        result = a && b;
    }

    [Node("OR", Category = "Logic", Description = "Logical OR operation")]
    public void Or(
        [Socket("Boolean", DefaultValue = false)] bool a,
        [Socket("Boolean", DefaultValue = false)] bool b,
        [Socket("Boolean")] out bool result)
    {
        result = a || b;
    }

    [Node("NOT", Category = "Logic", Description = "Logical NOT operation")]
    public void Not(
        [Socket("Boolean", DefaultValue = false)] bool value,
        [Socket("Boolean")] out bool result)
    {
        result = !value;
    }

    [Node("XOR", Category = "Logic", Description = "Logical XOR operation")]
    public void Xor(
        [Socket("Boolean", DefaultValue = false)] bool a,
        [Socket("Boolean", DefaultValue = false)] bool b,
        [Socket("Boolean")] out bool result)
    {
        result = a ^ b;
    }

    [Node("Greater Than", Category = "Logic", Description = "Checks if A > B")]
    public void GreaterThan(
        [Socket("Number", DefaultValue = 0)] double a,
        [Socket("Number", DefaultValue = 0)] double b,
        [Socket("Boolean")] out bool result)
    {
        result = a > b;
    }

    [Node("Less Than", Category = "Logic", Description = "Checks if A < B")]
    public void LessThan(
        [Socket("Number", DefaultValue = 0)] double a,
        [Socket("Number", DefaultValue = 0)] double b,
        [Socket("Boolean")] out bool result)
    {
        result = a < b;
    }

    [Node("Equals", Category = "Logic", Description = "Checks if A == B")]
    public void Equals(
        [Socket("Number", DefaultValue = 0)] double a,
        [Socket("Number", DefaultValue = 0)] double b,
        [Socket("Boolean")] out bool result)
    {
        result = Math.Abs(a - b) < 0.0001;
    }

    [Node("If-Then-Else", Category = "Logic", Description = "Returns True value if condition is true, else False value")]
    public void IfThenElse(
        [Socket("Boolean", DefaultValue = false)] bool condition,
        [Socket("Number", DefaultValue = 1)] double trueValue,
        [Socket("Number", DefaultValue = 0)] double falseValue,
        [Socket("Number")] out double result)
    {
        result = condition ? trueValue : falseValue;
    }
}
