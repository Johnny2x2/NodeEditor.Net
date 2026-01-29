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
                ExitPath.Signal();
            }
            else
            {
                LoopPath.Signal();
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

            if (!dynamicDict.ContainsKey(guid))
            {
                dynamicDict.Add(guid, 0);
                currentIndex = 0;
            }
            else
            {
                object temp;
                if (dynamicDict.TryGetValue(guid, out temp))
                {
                    currentIndex = (int)temp;
                }
            }

            if (currentIndex >= loopTimes.ToInt)
            {
                index = new nNum(currentIndex - 1);
                dynamicDict.Remove(guid);
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

            if (!dynamicDict.ContainsKey(guid))
            {
                dynamicDict.Add(guid, 0);
                currentIndex = 0;
            }
            else
            {
                object temp;
                if (dynamicDict.TryGetValue(guid, out temp))
                {
                    currentIndex = (int)temp;
                }
            }

            if (currentIndex < list.itemCount)
            {
                currentIndex++;
            }

            if (currentIndex >= list.itemCount)
            {
                obj = list.values[currentIndex - 1];
                ExitPath.Signal();
            }
            else
            {
                obj = list.values[currentIndex];
                dynamicDict[guid] = currentIndex;
                LoopPath.Signal();
            }
        }

        [Node("SignalNode", "Conditions", "Basic", "Signal Node", true)]
        public void SignalNode(out ExecutionPath nSignal)
        {
            nSignal = new ExecutionPath();
            nSignal.Signal();
        }
    }
}
