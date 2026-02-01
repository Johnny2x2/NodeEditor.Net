namespace NodeEditor
{
    public partial class StandardNodeContext
    {
        [Node("Branch", "Conditions", "Basic", "Branch Value on Bool", false)]
        public void Branch(ExecutionPath start, bool cond, out ExecutionPath True, out ExecutionPath False)
        {
            True = new ExecutionPath();
            False = new ExecutionPath();

            if (cond)
            {
                True.Signal();
            }
            else
            {
                False.Signal();
            }
        }

        [Node("Merge", "Conditions", "Basic", "Merge Paths To single Execution", false)]
        public void Merge(ExecutionPath Path1, ExecutionPath Path2, out ExecutionPath Exit)
        {
            Exit = new ExecutionPath();
            Exit.Signal();
        }

        [Node("While Loop", "Conditions/Loops", "Basic", "While loop", true)]
        public void WhileLoop(bool condition, ExecutionPath returnPath, out ExecutionPath ExitPath, out ExecutionPath LoopPath)
        {
            LoopPath = new ExecutionPath();
            returnPath = new ExecutionPath();
            ExitPath = new ExecutionPath();

            if (condition)
            {
                LoopPath.Signal();
            }
            else
            {
                ExitPath.Signal();
            }
        }

        [Node("For Loop", "Conditions/Loops", "Basic", "For loop", true)]
        public void ForLoop(nNum loopTimes, ExecutionPath Start, ExecutionPath returnPath, out ExecutionPath ExitPath, out ExecutionPath LoopPath, out nNum index)
        {
            LoopPath = new ExecutionPath();
            ExitPath = new ExecutionPath();
            returnPath = new ExecutionPath();

            int currentIndex = 0;

            string guid = CurrentProcessingNode.GetGuid();

            if (!dynamicDict.TryGetValue(guid, out var temp))
            {
                dynamicDict.TryAdd(guid, 0);
                currentIndex = 0;
            }
            else
            {
                currentIndex = (int)temp;
            }

            if (currentIndex >= loopTimes.ToInt)
            {
                index = new nNum(currentIndex - 1);
                dynamicDict.TryRemove(guid, out _);
                ExitPath.Signal();
            }
            else
            {
                index = new nNum(currentIndex);
                currentIndex++;
                dynamicDict[guid] = currentIndex;
                LoopPath.Signal();
            }
        }

        [Node("ForEach Loop", "Conditions/Loops", "Basic", "Branch Value on Bool", true)]
        public void ForEachLoop(SerializableList list, ExecutionPath returnPath, out ExecutionPath ExitPath, out ExecutionPath LoopPath, out object obj)
        {
            LoopPath = new ExecutionPath();
            ExitPath = new ExecutionPath();
            returnPath = new ExecutionPath();

            obj = new object();

            int currentIndex = 0;
            string guid = CurrentProcessingNode.GetGuid();

            if (!dynamicDict.TryGetValue(guid, out var temp))
            {
                dynamicDict.TryAdd(guid, 0);
                currentIndex = 0;
            }
            else
            {
                currentIndex = (int)temp;
            }

            if (currentIndex < list.Count)
            {
                currentIndex++;
            }

            if (currentIndex >= list.Count)
            {
                if (!list.TryGetAt(currentIndex - 1, out obj))
                {
                    obj = new object();
                }
                ExitPath.Signal();
            }
            else
            {
                if (!list.TryGetAt(currentIndex, out obj))
                {
                    obj = new object();
                }
                dynamicDict[guid] = currentIndex;
                LoopPath.Signal();
            }
        }

        [Node("For Loop Step", "Conditions/Loops", "Basic", "For loop with step.", true)]
        public void ForLoopStep(nNum start, nNum end, nNum step, ExecutionPath Start, ExecutionPath returnPath, out ExecutionPath ExitPath, out ExecutionPath LoopPath, out nNum index)
        {
            ReportRunning();
            LoopPath = new ExecutionPath();
            ExitPath = new ExecutionPath();
            returnPath = new ExecutionPath();

            var stepValue = step.ToDouble;
            var guid = CurrentProcessingNode.GetGuid();
            var key = $"{guid}:for-step";

            if (stepValue == 0)
            {
                index = new nNum(start.ToDouble);
                ExitPath.Signal();
                return;
            }

            double current;
            if (!dynamicDict.TryGetValue(key, out var temp))
            {
                current = start.ToDouble;
                dynamicDict.TryAdd(key, current);
            }
            else
            {
                current = (double)temp;
            }

            var shouldExit = stepValue > 0
                ? current > end.ToDouble
                : current < end.ToDouble;

            if (shouldExit)
            {
                index = new nNum(current - stepValue);
                dynamicDict.TryRemove(key, out _);
                ExitPath.Signal();
                return;
            }

            index = new nNum(current);
            current += stepValue;
            dynamicDict[key] = current;
            LoopPath.Signal();
        }

        [Node("Do While Loop", "Conditions/Loops", "Basic", "Do-while loop.", true)]
        public void DoWhileLoop(bool condition, ExecutionPath returnPath, out ExecutionPath ExitPath, out ExecutionPath LoopPath)
        {
            ReportRunning();
            LoopPath = new ExecutionPath();
            returnPath = new ExecutionPath();
            ExitPath = new ExecutionPath();

            var guid = CurrentProcessingNode.GetGuid();
            var key = $"{guid}:do-while";

            if (!dynamicDict.TryGetValue(key, out _))
            {
                dynamicDict.TryAdd(key, true);
                LoopPath.Signal();
                return;
            }

            if (condition)
            {
                LoopPath.Signal();
            }
            else
            {
                dynamicDict.TryRemove(key, out _);
                ExitPath.Signal();
            }
        }

        [Node("Repeat Until", "Conditions/Loops", "Basic", "Repeat until condition is true.", true)]
        public void RepeatUntil(bool condition, ExecutionPath returnPath, out ExecutionPath ExitPath, out ExecutionPath LoopPath)
        {
            ReportRunning();
            LoopPath = new ExecutionPath();
            returnPath = new ExecutionPath();
            ExitPath = new ExecutionPath();

            if (condition)
            {
                ExitPath.Signal();
            }
            else
            {
                LoopPath.Signal();
            }
        }

        [Node("SignalNode", "Conditions", "Basic", "Signal Node", true)]
        public void SignalNode(out ExecutionPath nSignal)
        {
            ReportRunning();
            nSignal = new ExecutionPath();
            nSignal.Signal();
        }
    }
}
