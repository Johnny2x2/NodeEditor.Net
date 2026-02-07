using System;
using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Execution;

public sealed partial class StandardNodeContext
{
    [Node("Branch", "Conditions", "Basic", "Branch value on bool.", false)]
    public void Branch(ExecutionPath Start, bool Cond, out ExecutionPath True, out ExecutionPath False)
    {
        ReportRunning();
        True = new ExecutionPath();
        False = new ExecutionPath();

        if (Cond)
        {
            True.Signal();
        }
        else
        {
            False.Signal();
        }
    }

    [Node("While Loop", "Conditions/Loops", "Basic", "While loop.", true)]
    public void WhileLoop(bool Condition, out ExecutionPath Exit, out ExecutionPath LoopPath)
    {
        ReportRunning();
        LoopPath = new ExecutionPath();
        Exit = new ExecutionPath();

        if (Condition)
        {
            LoopPath.Signal();
        }
        else
        {
            Exit.Signal();
        }
    }

    [Node("For Loop", "Conditions/Loops", "Basic", "For loop.", true)]
    public void ForLoop(int LoopTimes, out ExecutionPath Exit, out ExecutionPath LoopPath, out int Index)
    {
        ReportRunning();
        LoopPath = new ExecutionPath();
        Exit = new ExecutionPath();

        var key = GetStateKey("for");
        if (!TryGetState(key, out int currentIndex))
        {
            currentIndex = 0;
            SetState(key, currentIndex);
        }

        if (currentIndex >= LoopTimes)
        {
            Index = Math.Max(0, currentIndex - 1);
            ClearState(key);
            Exit.Signal();
        }
        else
        {
            Index = currentIndex;
            currentIndex++;
            SetState(key, currentIndex);
            LoopPath.Signal();
        }
    }

    [Node("ForEach Loop", "Conditions/Loops", "Basic", "Iterate list values.", true)]
    public void ForEachLoop(SerializableList List, out ExecutionPath Exit, out ExecutionPath LoopPath, out object Obj)
    {
        ReportRunning();
        LoopPath = new ExecutionPath();
        Exit = new ExecutionPath();
        Obj = new object();

        var key = GetStateKey("foreach");
        if (!TryGetState(key, out int currentIndex))
        {
            currentIndex = 0;
            SetState(key, currentIndex);
        }

        if (currentIndex < List.Count)
        {
            currentIndex++;
        }

        if (currentIndex >= List.Count)
        {
            if (!List.TryGetAt(currentIndex - 1, out Obj))
            {
                Obj = new object();
            }

            ClearState(key);
            Exit.Signal();
        }
        else
        {
            if (!List.TryGetAt(currentIndex, out Obj))
            {
                Obj = new object();
            }

            SetState(key, currentIndex);
            LoopPath.Signal();
        }
    }

    [Node("For Loop Step", "Conditions/Loops", "Basic", "For loop with step.", true)]
    public void ForLoopStep(int StartValue, int EndValue, int Step, out ExecutionPath Exit, out ExecutionPath LoopPath, out int Index)
    {
        ReportRunning();
        LoopPath = new ExecutionPath();
        Exit = new ExecutionPath();

        if (Step == 0)
        {
            Index = StartValue;
            Exit.Signal();
            return;
        }

        var key = GetStateKey("for-step");
        if (!TryGetState(key, out int current))
        {
            current = StartValue;
            SetState(key, current);
        }

        var shouldExit = Step > 0 ? current > EndValue : current < EndValue;
        if (shouldExit)
        {
            Index = current - Step;
            ClearState(key);
            Exit.Signal();
            return;
        }

        Index = current;
        current += Step;
        SetState(key, current);
        LoopPath.Signal();
    }

    [Node("Do While Loop", "Conditions/Loops", "Basic", "Do-while loop.", true)]
    public void DoWhileLoop(bool Condition, out ExecutionPath Exit, out ExecutionPath LoopPath)
    {
        ReportRunning();
        LoopPath = new ExecutionPath();
        Exit = new ExecutionPath();

        var key = GetStateKey("do-while");
        if (!TryGetState(key, out bool _))
        {
            SetState(key, true);
            LoopPath.Signal();
            return;
        }

        if (Condition)
        {
            LoopPath.Signal();
        }
        else
        {
            ClearState(key);
            Exit.Signal();
        }
    }

    [Node("Repeat Until", "Conditions/Loops", "Basic", "Repeat until condition is true.", true)]
    public void RepeatUntil(bool Condition, out ExecutionPath Exit, out ExecutionPath LoopPath)
    {
        ReportRunning();
        LoopPath = new ExecutionPath();
        Exit = new ExecutionPath();

        if (Condition)
        {
            Exit.Signal();
        }
        else
        {
            LoopPath.Signal();
        }
    }
}
