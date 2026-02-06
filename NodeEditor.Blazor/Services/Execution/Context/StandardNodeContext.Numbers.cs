using System;

namespace NodeEditor.Blazor.Services.Execution;

public sealed partial class StandardNodeContext
{
    private static readonly object RandomLock = new();
    private static readonly Random RandomGenerator = new();

    [Node("Abs", "Numbers", "Basic", "Absolute value.", false)]
    public void NumAbs(double Value, out double Result)
    {
        ReportRunning();
        Result = Math.Abs(Value);
    }

    [Node("Min", "Numbers", "Basic", "Minimum of two numbers.", false)]
    public void NumMin(double A, double B, out double Result)
    {
        ReportRunning();
        Result = Math.Min(A, B);
    }

    [Node("Max", "Numbers", "Basic", "Maximum of two numbers.", false)]
    public void NumMax(double A, double B, out double Result)
    {
        ReportRunning();
        Result = Math.Max(A, B);
    }

    [Node("Mod", "Numbers", "Basic", "Remainder of division.", false)]
    public void NumMod(double A, double B, out double Result)
    {
        ReportRunning();
        Result = A % B;
    }

    [Node("Round", "Numbers", "Basic", "Round to number of digits.", false)]
    public void NumRound(double Value, int Digits, out double Result)
    {
        ReportRunning();
        Result = Math.Round(Value, Math.Max(0, Digits));
    }

    [Node("Floor", "Numbers", "Basic", "Floor value.", false)]
    public void NumFloor(double Value, out double Result)
    {
        ReportRunning();
        Result = Math.Floor(Value);
    }

    [Node("Ceiling", "Numbers", "Basic", "Ceiling value.", false)]
    public void NumCeiling(double Value, out double Result)
    {
        ReportRunning();
        Result = Math.Ceiling(Value);
    }

    [Node("Clamp", "Numbers", "Basic", "Clamp value between min and max.", false)]
    public void NumClamp(double Value, double Min, double Max, out double Result)
    {
        ReportRunning();
        var minVal = Math.Min(Min, Max);
        var maxVal = Math.Max(Min, Max);
        Result = Math.Min(Math.Max(Value, minVal), maxVal);
    }

    [Node("Random Range", "Numbers", "Basic", "Random number between min and max.", false)]
    public void NumRandomRange(double Min, double Max, out double Result)
    {
        ReportRunning();
        var minVal = Math.Min(Min, Max);
        var maxVal = Math.Max(Min, Max);

        lock (RandomLock)
        {
            Result = RandomGenerator.NextDouble() * (maxVal - minVal) + minVal;
        }
    }

    [Node("Sign", "Numbers", "Basic", "Get sign of a value.", false)]
    public void NumSign(double Value, out int Sign)
    {
        ReportRunning();
        Sign = Math.Sign(Value);
    }
}
