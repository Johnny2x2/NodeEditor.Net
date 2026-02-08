using System.Text.Json;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.Services.Execution;
using NodeEditor.Blazor.Services.Serialization;

namespace NodeEditor.Mcp.Abilities;

/// <summary>
/// Provides abilities for running and controlling graph execution.
/// </summary>
public sealed class ExecutionAbilityProvider : IAbilityProvider
{
    private readonly INodeEditorState _state;
    private readonly INodeExecutionService _executionService;
    private readonly HeadlessGraphRunner _headlessRunner;
    private readonly IGraphSerializer _serializer;
    private CancellationTokenSource? _currentCts;
    private string _executionStatus = "idle";
    private string? _executionError;

    public ExecutionAbilityProvider(
        INodeEditorState state,
        INodeExecutionService executionService,
        HeadlessGraphRunner headlessRunner,
        IGraphSerializer serializer)
    {
        _state = state;
        _executionService = executionService;
        _headlessRunner = headlessRunner;
        _serializer = serializer;

        _executionService.NodeStarted += (_, e) => _executionStatus = $"running (node: {e.Node.Name})";
        _executionService.NodeFailed += (_, e) => _executionError = $"Node '{e.Node.Name}' failed: {e.Exception.Message}";
        _executionService.ExecutionFailed += (_, e) =>
        {
            _executionStatus = "failed";
            _executionError = e.Message;
        };
        _executionService.ExecutionCanceled += (_, _) => _executionStatus = "canceled";
    }

    public string Source => "Core";

    public IReadOnlyList<AbilityDescriptor> GetAbilities() =>
    [
        new("execution.run", "Run Graph", "Execution",
            "Executes the current graph.",
            "Starts execution of the graph currently on the canvas. " +
            "Execution runs asynchronously; use execution.status to poll progress.",
            [],
            ReturnDescription: "Execution started confirmation."),

        new("execution.run_json", "Run Graph from JSON", "Execution",
            "Executes a graph from a JSON string without affecting the canvas.",
            "Provide the graph JSON to execute headlessly.",
            [new("json", "string", "The graph JSON to execute.")],
            ReturnDescription: "Execution result."),

        new("execution.stop", "Stop Execution", "Execution",
            "Stops the currently running graph execution.",
            "Cancels the running execution if one is active.",
            []),

        new("execution.status", "Execution Status", "Execution",
            "Returns the current execution status.",
            "Shows whether execution is idle, running, completed, failed, or canceled.",
            [],
            ReturnDescription: "Current execution status string and any error details."),

        new("execution.pause", "Pause Execution", "Execution",
            "Pauses the currently running execution at the next step boundary.",
            "Uses the execution gate to pause stepping.",
            []),

        new("execution.resume", "Resume Execution", "Execution",
            "Resumes a paused execution.",
            "Releases the execution gate to continue stepping.",
            []),

        new("execution.step", "Step Execution", "Execution",
            "Advances a paused execution by one step.",
            "The execution gate advances one step then pauses again.",
            [])
    ];

    public async Task<AbilityResult> ExecuteAsync(string abilityId, JsonElement parameters, CancellationToken cancellationToken = default)
    {
        return abilityId switch
        {
            "execution.run" => await RunGraph(cancellationToken),
            "execution.run_json" => await RunFromJson(parameters, cancellationToken),
            "execution.stop" => StopExecution(),
            "execution.status" => GetStatus(),
            "execution.pause" => PauseExecution(),
            "execution.resume" => ResumeExecution(),
            "execution.step" => StepExecution(),
            _ => new AbilityResult(false, $"Unknown ability: {abilityId}")
        };
    }

    private async Task<AbilityResult> RunGraph(CancellationToken cancellationToken)
    {
        if (_executionStatus == "running")
            return new AbilityResult(false, "Execution is already running. Use execution.stop first.");

        _currentCts?.Dispose();
        _currentCts = new CancellationTokenSource();
        _executionStatus = "running";
        _executionError = null;

        try
        {
            var graphData = _state.ExportToGraphData();
            _ = Task.Run(async () =>
            {
                try
                {
                    await _headlessRunner.ExecuteAsync(graphData, cancellationToken: _currentCts.Token);
                    _executionStatus = "completed";
                }
                catch (OperationCanceledException)
                {
                    _executionStatus = "canceled";
                }
                catch (Exception ex)
                {
                    _executionStatus = "failed";
                    _executionError = ex.Message;
                }
            }, cancellationToken);

            return new AbilityResult(true, "Execution started. Use execution.status to poll progress.");
        }
        catch (Exception ex)
        {
            _executionStatus = "failed";
            _executionError = ex.Message;
            return new AbilityResult(false, $"Failed to start execution: {ex.Message}");
        }
    }

    private async Task<AbilityResult> RunFromJson(JsonElement p, CancellationToken ct)
    {
        if (!p.TryGetProperty("json", out var jsonEl))
            return new AbilityResult(false, "Missing required parameter 'json'.");

        try
        {
            var context = await _headlessRunner.ExecuteFromJsonAsync(jsonEl.GetString()!, cancellationToken: ct);
            return new AbilityResult(true, "Headless execution completed.",
                Data: new { Status = "completed" });
        }
        catch (Exception ex)
        {
            return new AbilityResult(false, $"Headless execution failed: {ex.Message}");
        }
    }

    private AbilityResult StopExecution()
    {
        if (_currentCts is null || _executionStatus != "running")
            return new AbilityResult(false, "No execution is currently running.");

        _currentCts.Cancel();
        _executionStatus = "canceling";
        return new AbilityResult(true, "Execution stop requested.");
    }

    private AbilityResult GetStatus()
    {
        return new AbilityResult(true, Data: new
        {
            Status = _executionStatus,
            Error = _executionError,
            GateState = _executionService.Gate.State.ToString()
        });
    }

    private AbilityResult PauseExecution()
    {
        _executionService.Gate.Pause();
        return new AbilityResult(true, "Execution paused.");
    }

    private AbilityResult ResumeExecution()
    {
        _executionService.Gate.Resume();
        return new AbilityResult(true, "Execution resumed.");
    }

    private AbilityResult StepExecution()
    {
        _executionService.Gate.StepOnce();
        return new AbilityResult(true, "Stepped one step.");
    }
}
