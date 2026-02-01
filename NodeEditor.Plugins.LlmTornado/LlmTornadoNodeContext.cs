using System.Reflection;
using LlmTornado;
using LlmTornado.Agents;
using LlmTornado.Agents.ChatRuntime;
using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.DataModels;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Mcp;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services.Execution;
using NodeEditor.Blazor.Services.Registry;

namespace NodeEditor.Plugins.LlmTornado;

public sealed class LlmTornadoNodeContext : INodeMethodContext, INodeContext
{
    public NodeData? CurrentProcessingNode { get; set; }

    public event Action<string, NodeData, ExecutionFeedbackType, object?, bool>? FeedbackInfo;

    private void ReportRunning(string message = "Running")
    {
        if (CurrentProcessingNode is null)
        {
            return;
        }

        FeedbackInfo?.Invoke(message, CurrentProcessingNode, ExecutionFeedbackType.None, null, false);
    }

    private ValueTask ForwardAgentEvent(AgentRunnerEvents agentEvent)
    {
        if (CurrentProcessingNode is null)
        {
            return ValueTask.CompletedTask;
        }

        var message = agentEvent.GetType().Name;
        FeedbackInfo?.Invoke(message, CurrentProcessingNode, ExecutionFeedbackType.None, agentEvent, false);
        return ValueTask.CompletedTask;
    }

    [Node("Create Tornado API", category: "LLM Tornado/Agents", description: "Create a Tornado API client.", isCallable: false)]
    public void CreateTornadoApi(string ApiKey, string? BaseUrl, out TornadoApi Client)
    {
        Client = CreateTornadoApiInstance(ApiKey, BaseUrl);
    }

    [Node("Create Chat Model", category: "LLM Tornado/Agents", description: "Create a chat model by name.", isCallable: false)]
    public void CreateChatModel(string ModelName, out ChatModel Model)
    {
        Model = CreateChatModelInstance(ModelName);
    }

    [Node("Create Agent", category: "LLM Tornado/Agents", description: "Create a Tornado agent.", isCallable: false)]
    public void CreateAgent(TornadoApi Client, ChatModel Model, string Name, string Instructions, bool Streaming, out TornadoAgent Agent)
    {
        var safeName = string.IsNullOrWhiteSpace(Name) ? "Assistant" : Name;
        var safeInstructions = string.IsNullOrWhiteSpace(Instructions) ? "You are a helpful assistant." : Instructions;
        Agent = new TornadoAgent(Client, Model, safeName, safeInstructions, streaming: Streaming);
    }

    [Node("Add Tools", category: "LLM Tornado/Agents", description: "Add tools (including MCP tools) to an agent.", isCallable: false)]
    public void AddTools(TornadoAgent Agent, List<Tool> Tools, out TornadoAgent Updated)
    {
        Agent.AddTool(Tools);
        Updated = Agent;
    }

    [Node("Run Agent", category: "LLM Tornado/Agents", description: "Run agent with text input (returns task).", isCallable: true)]
    public void RunAgentAsync(
        ExecutionPath Enter,
        TornadoAgent Agent,
        string Input,
        List<ChatMessage>? AppendMessages,
        bool Streaming,
        bool SingleTurn,
        out Task<ChatMessage> ResultTask,
        out ExecutionPath Exit,
        CancellationToken token)
    {
        ReportRunning();

        var inputText = string.IsNullOrWhiteSpace(Input) ? null : Input;
        ResultTask = Agent.Run(
                input: inputText,
                appendMessages: AppendMessages,
                streaming: Streaming,
                onAgentRunnerEvent: ForwardAgentEvent,
                singleTurn: SingleTurn,
                cancellationToken: token)
            .ContinueWith(
                task => task.Result?.Messages?.LastOrDefault() ?? new ChatMessage(),
                token,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

        Exit = new ExecutionPath();
        Exit.Signal();
    }

    [Node("Create Text Part", category: "LLM Tornado/Messages", description: "Create a text message part.", isCallable: false)]
    public void CreateTextPart(string Text, out ChatMessagePart Part)
    {
        Part = new ChatMessagePart(Text ?? string.Empty);
    }

    [Node("Create Image Part", category: "LLM Tornado/Messages", description: "Create an image message part from URL.", isCallable: false)]
    public void CreateImagePart(string ImageUrl, out ChatMessagePart Part)
    {
        Part = new ChatMessagePart(new Uri(ImageUrl));
    }

    [Node("Create User Message", category: "LLM Tornado/Messages", description: "Create a user message from text.", isCallable: false)]
    public void CreateUserMessage(string Text, out ChatMessage Message)
    {
        Message = new ChatMessage(ChatMessageRoles.User, Text ?? string.Empty);
    }

    [Node("Create User Message (Parts)", category: "LLM Tornado/Messages", description: "Create a user message from parts (text/images).", isCallable: false)]
    public void CreateUserMessageFromParts(List<ChatMessagePart> Parts, out ChatMessage Message)
    {
        Message = new ChatMessage(ChatMessageRoles.User, Parts ?? new List<ChatMessagePart>());
    }

    [Node("Create Assistant Message", category: "LLM Tornado/Messages", description: "Create an assistant message from text.", isCallable: false)]
    public void CreateAssistantMessage(string Text, out ChatMessage Message)
    {
        Message = new ChatMessage(ChatMessageRoles.Assistant, Text ?? string.Empty);
    }

    [Node("Message To Text", category: "LLM Tornado/Messages", description: "Extract text from a chat message.", isCallable: false)]
    public void MessageToText(ChatMessage Message, out string Text)
    {
        Text = ExtractMessageText(Message);
    }

    [Node("MCP Toolkit (Puppeteer)", category: "LLM Tornado/MCP", description: "Create MCP Puppeteer toolkit server.", isCallable: false)]
    public void McpToolkitPuppeteer(out MCPServer Server)
    {
        Server = MCPToolkits.Puppeteer();
    }

    [Node("MCP Toolkit (FileSystem)", category: "LLM Tornado/MCP", description: "Create MCP FileSystem toolkit server.", isCallable: false)]
    public void McpToolkitFileSystem(string WorkspaceFolder, out MCPServer Server)
    {
        Server = MCPToolkits.FileSystem(WorkspaceFolder);
    }

    [Node("MCP Toolkit (GitHub)", category: "LLM Tornado/MCP", description: "Create MCP GitHub toolkit server.", isCallable: false)]
    public void McpToolkitGithub(string ApiKey, out MCPServer Server)
    {
        Server = MCPToolkits.Github(ApiKey);
    }

    [Node("MCP Toolkit (Playwright)", category: "LLM Tornado/MCP", description: "Create MCP Playwright toolkit server.", isCallable: false)]
    public void McpToolkitPlaywright(out MCPServer Server)
    {
        Server = MCPToolkits.Playwright();
    }

    [Node("MCP Toolkit (Fetch)", category: "LLM Tornado/MCP", description: "Create MCP Fetch toolkit server.", isCallable: false)]
    public void McpToolkitFetch(out MCPServer Server)
    {
        Server = MCPToolkits.Fetch();
    }

    [Node("MCP Initialize", category: "LLM Tornado/MCP", description: "Initialize MCP server and load tools (returns task).", isCallable: false)]
    public void InitializeMcpServerAsync(MCPServer Server, out Task<List<Tool>> ToolsTask, CancellationToken token)
    {
        ReportRunning();
        token.ThrowIfCancellationRequested();

        ToolsTask = Server.InitializeAsync()
            .ContinueWith(
                _ => Server.AllowedTornadoTools,
                token,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    [Node("Await Tools", category: "LLM Tornado/MCP", description: "Await MCP tools task and return tools.", isCallable: false)]
    public void AwaitTools(Task<List<Tool>> ToolsTask, out List<Tool> Tools)
    {
        Tools = ToolsTask.GetAwaiter().GetResult();
    }

    [Node("Create Orchestration Runtime", category: "LLM Tornado/Orchestration", description: "Create an orchestration runtime configuration.", isCallable: false)]
    public void CreateOrchestrationRuntime(string? MessageHistoryFile, out OrchestrationRuntimeConfiguration Runtime)
    {
        Runtime = new OrchestrationRuntimeConfiguration();
        if (!string.IsNullOrWhiteSpace(MessageHistoryFile))
        {
            Runtime.MessageHistoryFileLocation = MessageHistoryFile;
        }
    }

    [Node("Create Chat Runtime", category: "LLM Tornado/Orchestration", description: "Create chat runtime from configuration.", isCallable: false)]
    public void CreateChatRuntime(IRuntimeConfiguration Configuration, out ChatRuntime Runtime)
    {
        Runtime = new ChatRuntime(Configuration);
    }

    [Node("Create Agent Runnable", category: "LLM Tornado/Orchestration", description: "Create an orchestration runnable that uses an agent.", isCallable: false)]
    public void CreateAgentRunnable(OrchestrationRuntimeConfiguration Orchestration, TornadoAgent Agent, string Name, bool AllowDeadEnd, out AgentChatRunnable Runnable)
    {
        Runnable = new AgentChatRunnable(Orchestration, Agent, Name);
        Runnable.AllowDeadEnd = AllowDeadEnd;
    }

    [Node("Set Entry Runnable", category: "LLM Tornado/Orchestration", description: "Set the entry runnable for orchestration.", isCallable: false)]
    public void SetEntryRunnable(OrchestrationRuntimeConfiguration Orchestration, OrchestrationRunnableBase Runnable, out OrchestrationRuntimeConfiguration Updated)
    {
        Orchestration.SetEntryRunnable(Runnable);
        Updated = Orchestration;
    }

    [Node("Set Result Runnable", category: "LLM Tornado/Orchestration", description: "Set the result runnable for orchestration.", isCallable: false)]
    public void SetResultRunnable(OrchestrationRuntimeConfiguration Orchestration, OrchestrationRunnableBase Runnable, out OrchestrationRuntimeConfiguration Updated)
    {
        Orchestration.SetRunnableWithResult(Runnable);
        Updated = Orchestration;
    }

    [Node("Add Advancer", category: "LLM Tornado/Orchestration", description: "Add a conditional transition between runnables.", isCallable: false)]
    public void AddAdvancer(OrchestrationRunnableBase From, OrchestrationRunnableBase To, bool Condition, out OrchestrationRunnableBase Updated)
    {
        var advancer = new OrchestrationAdvancer<ChatMessage>(To, _ => Condition);
        AddAdvancerInternal(From, advancer);
        Updated = From;
    }

    [Node("Invoke Orchestration", category: "LLM Tornado/Orchestration", description: "Run orchestration and return final output (returns task).", isCallable: true)]
    public void InvokeOrchestrationAsync(
        ExecutionPath Enter,
        OrchestrationRuntimeConfiguration Orchestration,
        ChatMessage Input,
        out Task<ChatMessage> ResultTask,
        out ExecutionPath Exit,
        CancellationToken token)
    {
        ReportRunning();
        token.ThrowIfCancellationRequested();

        ResultTask = Orchestration.InvokeAsync(Input)
            .ContinueWith(
                task => task.Result?.LastOrDefault() ?? new ChatMessage(),
                token,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

        Exit = new ExecutionPath();
        Exit.Signal();
    }

    [Node("Invoke Chat Runtime", category: "LLM Tornado/Orchestration", description: "Invoke chat runtime and return response (returns task).", isCallable: true)]
    public void InvokeChatRuntimeAsync(
        ExecutionPath Enter,
        ChatRuntime Runtime,
        ChatMessage Input,
        out Task<ChatMessage> ResultTask,
        out ExecutionPath Exit,
        CancellationToken token)
    {
        ReportRunning();
        token.ThrowIfCancellationRequested();

        ResultTask = Runtime.InvokeAsync(Input);

        Exit = new ExecutionPath();
        Exit.Signal();
    }

    [Node("Await Chat Message", category: "LLM Tornado/Messages", description: "Await a chat message task.", isCallable: false)]
    public void AwaitChatMessage(Task<ChatMessage> MessageTask, out ChatMessage Message)
    {
        Message = MessageTask.GetAwaiter().GetResult();
    }

    private static string ExtractMessageText(ChatMessage message)
    {
        if (message is null)
        {
            return string.Empty;
        }

        var type = message.GetType();
        var contentProperty = type.GetProperty("Content", BindingFlags.Instance | BindingFlags.Public)
                              ?? type.GetProperty("Text", BindingFlags.Instance | BindingFlags.Public)
                              ?? type.GetProperty("Message", BindingFlags.Instance | BindingFlags.Public);

        if (contentProperty?.GetValue(message) is string content)
        {
            return content;
        }

        return message.ToString() ?? string.Empty;
    }

    private static TornadoApi CreateTornadoApiInstance(string apiKey, string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key is required.", nameof(apiKey));
        }

        var type = typeof(TornadoApi);
        var constructors = type.GetConstructors();

        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            var ctor = constructors.FirstOrDefault(c => MatchesParameters(c, typeof(string), typeof(string)))
                       ?? constructors.FirstOrDefault(c => MatchesParameters(c, typeof(string), typeof(Uri)));

            if (ctor != null)
            {
                var args = ctor.GetParameters().Length == 2 && ctor.GetParameters()[1].ParameterType == typeof(Uri)
                    ? new object?[] { apiKey, new Uri(baseUrl) }
                    : new object?[] { apiKey, baseUrl };
                return (TornadoApi)ctor.Invoke(args);
            }
        }

        var singleCtor = constructors.FirstOrDefault(c => MatchesParameters(c, typeof(string)));
        if (singleCtor != null)
        {
            return (TornadoApi)singleCtor.Invoke(new object?[] { apiKey });
        }

        var staticFactory = type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, binder: null, types: new[] { typeof(string) }, modifiers: null)
                   ?? type.GetMethod("FromApiKey", BindingFlags.Public | BindingFlags.Static, binder: null, types: new[] { typeof(string) }, modifiers: null)
                   ?? type.GetMethod("FromKey", BindingFlags.Public | BindingFlags.Static, binder: null, types: new[] { typeof(string) }, modifiers: null);

        if (staticFactory != null)
        {
            return (TornadoApi)staticFactory.Invoke(null, new object?[] { apiKey })!;
        }

        throw new InvalidOperationException("Unable to create TornadoApi instance. No supported constructor or factory method found.");
    }

    private static ChatModel CreateChatModelInstance(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new ArgumentException("Model name is required.", nameof(modelName));
        }

        var type = typeof(ChatModel);
        var fromName = type.GetMethod("FromName", BindingFlags.Public | BindingFlags.Static, binder: null, types: new[] { typeof(string) }, modifiers: null)
                   ?? type.GetMethod("GetByName", BindingFlags.Public | BindingFlags.Static, binder: null, types: new[] { typeof(string) }, modifiers: null)
                   ?? type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, binder: null, types: new[] { typeof(string) }, modifiers: null);

        if (fromName != null)
        {
            return (ChatModel)fromName.Invoke(null, new object?[] { modelName })!;
        }

        var ctor = type.GetConstructor(new[] { typeof(string) });
        if (ctor != null)
        {
            return (ChatModel)ctor.Invoke(new object?[] { modelName });
        }

        throw new InvalidOperationException("Unable to create ChatModel instance. No supported constructor or factory method found.");
    }

    private static void AddAdvancerInternal(OrchestrationRunnableBase from, OrchestrationAdvancer advancer)
    {
        var method = typeof(OrchestrationRunnableBase)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "AddAdvancer"
                                 && m.GetParameters().Length == 1
                                 && m.GetParameters()[0].ParameterType == typeof(OrchestrationAdvancer));
        if (method is null)
        {
            throw new InvalidOperationException("Unable to add advancer. Internal method not found.");
        }

        method.Invoke(from, new object[] { advancer });
    }

    private static bool MatchesParameters(ConstructorInfo ctor, params Type[] parameterTypes)
    {
        var parameters = ctor.GetParameters();
        if (parameters.Length != parameterTypes.Length)
        {
            return false;
        }

        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType != parameterTypes[i])
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class AgentChatRunnable : OrchestrationRunnable<ChatMessage, ChatMessage>
{
    private readonly TornadoAgent _agent;
    private readonly Func<AgentRunnerEvents, ValueTask>? _eventCallback;

    public AgentChatRunnable(
        OrchestrationRuntimeConfiguration orchestrator,
        TornadoAgent agent,
        string? name = null,
        Func<AgentRunnerEvents, ValueTask>? eventCallback = null)
        : base(orchestrator, name ?? string.Empty)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _eventCallback = eventCallback;
    }

    public override async ValueTask<ChatMessage> Invoke(RunnableProcess<ChatMessage, ChatMessage> input)
    {
        var message = input.Input ?? new ChatMessage();
        var conversation = await _agent.Run(
            input: (string?)null,
            appendMessages: [message],
            onAgentRunnerEvent: _eventCallback,
            cancellationToken: Orchestrator?.CancelToken ?? CancellationToken.None)
            .ConfigureAwait(false);

        return conversation?.Messages?.LastOrDefault() ?? new ChatMessage();
    }
}
