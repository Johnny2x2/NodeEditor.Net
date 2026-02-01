namespace NodeEditor
{
    public partial class StandardNodeContext
    {
        [Node("Parallel Split", "Parallel", "Basic", "Signal multiple execution paths.", false)]
        public void ParallelSplit(ExecutionPath start, out ExecutionPath PathA, out ExecutionPath PathB)
        {
            ReportRunning();
            PathA = new ExecutionPath();
            PathB = new ExecutionPath();

            if (start?.IsSignaled != false)
            {
                PathA.Signal();
                PathB.Signal();
            }
        }

        [Node("Parallel Join", "Parallel", "Basic", "Wait for multiple paths to complete.", false)]
        public void ParallelJoin(ExecutionPath PathA, ExecutionPath PathB, out ExecutionPath Exit)
        {
            ReportRunning();
            Exit = new ExecutionPath();

            var guid = CurrentProcessingNode.GetGuid();
            var key = $"{guid}:parallel-join";
            var mask = 0;

            if (dynamicDict.TryGetValue(key, out var temp))
            {
                mask = (int)temp;
            }

            if (PathA?.IsSignaled == true)
            {
                mask |= 1;
            }

            if (PathB?.IsSignaled == true)
            {
                mask |= 2;
            }

            dynamicDict[key] = mask;

            if (mask == 3)
            {
                dynamicDict.TryRemove(key, out _);
                Exit.Signal();
            }
        }
    }
}
