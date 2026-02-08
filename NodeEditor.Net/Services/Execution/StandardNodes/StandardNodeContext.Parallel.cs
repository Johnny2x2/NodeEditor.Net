namespace NodeEditor.Net.Services.Execution;

// ParallelSplit and ParallelJoin have been removed.
// The unified execution engine handles parallelism automatically via
// topological layer detection — independent nodes in the same layer
// execute concurrently without requiring explicit split/join wiring.

