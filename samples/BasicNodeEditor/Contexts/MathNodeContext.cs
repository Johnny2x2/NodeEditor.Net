using NodeEditor.Blazor;

namespace BasicNodeEditor.Contexts;

/// <summary>
/// Provides basic mathematical operations as nodes.
/// </summary>
public class MathNodeContext : INodeContext
{
    [Node("Add", Category = "Math", Description = "Adds two numbers")]
    public void Add(
        [Socket("Number", DefaultValue = 0)] int a,
        [Socket("Number", DefaultValue = 0)] int b,
        [Socket("Number")] out int result)
    {
        result = a + b;
    }

    [Node("Subtract", Category = "Math", Description = "Subtracts B from A")]
    public void Subtract(
        [Socket("Number", DefaultValue = 0)] int a,
        [Socket("Number", DefaultValue = 0)] int b,
        [Socket("Number")] out int result)
    {
        result = a - b;
    }

    [Node("Multiply", Category = "Math", Description = "Multiplies two numbers")]
    public void Multiply(
        [Socket("Number", DefaultValue = 1)] int a,
        [Socket("Number", DefaultValue = 1)] int b,
        [Socket("Number")] out int result)
    {
        result = a * b;
    }

    [Node("Divide", Category = "Math", Description = "Divides A by B")]
    public void Divide(
        [Socket("Number", DefaultValue = 1)] double a,
        [Socket("Number", DefaultValue = 1)] double b,
        [Socket("Number")] out double result)
    {
        result = b != 0 ? a / b : 0;
    }

    [Node("Power", Category = "Math", Description = "Raises A to the power of B")]
    public void Power(
        [Socket("Number", DefaultValue = 2)] double a,
        [Socket("Number", DefaultValue = 2)] double b,
        [Socket("Number")] out double result)
    {
        result = Math.Pow(a, b);
    }

    [Node("Square Root", Category = "Math", Description = "Calculates square root")]
    public void SquareRoot(
        [Socket("Number", DefaultValue = 4)] double value,
        [Socket("Number")] out double result)
    {
        result = Math.Sqrt(value);
    }

    [Node("Absolute Value", Category = "Math", Description = "Returns absolute value")]
    public void Abs(
        [Socket("Number", DefaultValue = 0)] double value,
        [Socket("Number")] out double result)
    {
        result = Math.Abs(value);
    }

    [Node("Min", Category = "Math", Description = "Returns minimum of two numbers")]
    public void Min(
        [Socket("Number", DefaultValue = 0)] double a,
        [Socket("Number", DefaultValue = 0)] double b,
        [Socket("Number")] out double result)
    {
        result = Math.Min(a, b);
    }

    [Node("Max", Category = "Math", Description = "Returns maximum of two numbers")]
    public void Max(
        [Socket("Number", DefaultValue = 0)] double a,
        [Socket("Number", DefaultValue = 0)] double b,
        [Socket("Number")] out double result)
    {
        result = Math.Max(a, b);
    }

    [Node("Clamp", Category = "Math", Description = "Clamps value between min and max")]
    public void Clamp(
        [Socket("Number", DefaultValue = 50)] double value,
        [Socket("Number", DefaultValue = 0)] double min,
        [Socket("Number", DefaultValue = 100)] double max,
        [Socket("Number")] out double result)
    {
        result = Math.Clamp(value, min, max);
    }
}
