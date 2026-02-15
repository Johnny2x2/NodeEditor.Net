using LlmTornado;
using LlmTornado.Code;
using LlmTornado.Common;
using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Registry;
using NodeEditor.Plugins.LLMTornado;
using NodeEditor.Plugins.LLMTornado.Nodes;
using NodeEditor.Plugins.LLMTornado.Services;
using System.Net;
using System.Net.Http;
using System.Text;

namespace NodeEditor.Blazor.Tests;

public sealed class LLMTornadoPluginTests
{
    [Fact]
    public void Plugin_Metadata_IsExpected()
    {
        var plugin = new LLMTornadoPlugin();

        Assert.Equal("LLMTornado Plugin", plugin.Name);
        Assert.Equal("com.nodeeditormax.llmtornado", plugin.Id);
        Assert.Equal(new Version(2, 0, 0), plugin.Version);
        Assert.Equal(new Version(1, 0, 0), plugin.MinApiVersion);
    }

    [Fact]
    public void Plugin_ConfigureServices_RegistersRequiredServices()
    {
        var plugin = new LLMTornadoPlugin();
        var services = new ServiceCollection();

        plugin.ConfigureServices(services);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<LLMTornadoConfigResolver>());
        Assert.NotNull(provider.GetService<ILLMTornadoApiFactory>());
    }

    [Fact]
    public void Plugin_Register_RegistersAllNodeDefinitions()
    {
        var plugin = new LLMTornadoPlugin();
        var registry = new NodeRegistryService(new NodeDiscoveryService());

        plugin.Register(registry);

        Assert.Contains(registry.Definitions, d => d.NodeType == typeof(SimpleChatNode));
        Assert.Contains(registry.Definitions, d => d.NodeType == typeof(CreateEmbeddingNode));
        Assert.Contains(registry.Definitions, d => d.NodeType == typeof(ListModelsNode));
        Assert.Contains(registry.Definitions, d => d.NodeType == typeof(CreateResponseNode));
        Assert.Contains(registry.Definitions, d => d.NodeType == typeof(StreamChatNode));
        Assert.Contains(registry.Definitions, d => d.NodeType == typeof(StreamResponseNode));
        Assert.Contains(registry.Definitions, d => d.NodeType == typeof(GenerateImageNode));
        Assert.Contains(registry.Definitions, d => d.NodeType == typeof(LoadImageNode));
        Assert.Contains(registry.Definitions, d => d.NodeType == typeof(LoadImageFromUrlNode));
    }

    [Fact]
    public void ConfigResolver_ReadsEnvironmentVariables()
    {
        const string providerKey = "LLMTORNADO_PROVIDER";
        const string apiKeyKey = "LLMTORNADO_API_KEY";
        const string orgKey = "LLMTORNADO_ORGANIZATION";
        const string baseUrlKey = "LLMTORNADO_BASE_URL";
        const string versionKey = "LLMTORNADO_API_VERSION";

        var oldProvider = Environment.GetEnvironmentVariable(providerKey);
        var oldApiKey = Environment.GetEnvironmentVariable(apiKeyKey);
        var oldOrg = Environment.GetEnvironmentVariable(orgKey);
        var oldBaseUrl = Environment.GetEnvironmentVariable(baseUrlKey);
        var oldVersion = Environment.GetEnvironmentVariable(versionKey);

        try
        {
            Environment.SetEnvironmentVariable(providerKey, "Groq");
            Environment.SetEnvironmentVariable(apiKeyKey, "test-key");
            Environment.SetEnvironmentVariable(orgKey, "org-1");
            Environment.SetEnvironmentVariable(baseUrlKey, "https://example.local/{0}/{1}");
            Environment.SetEnvironmentVariable(versionKey, "v9");

            var resolver = new LLMTornadoConfigResolver();
            var config = resolver.Resolve();

            Assert.Equal("Groq", config.Provider);
            Assert.Equal("test-key", config.ApiKey);
            Assert.Equal("org-1", config.Organization);
            Assert.Equal("https://example.local/{0}/{1}", config.BaseUrl);
            Assert.Equal("v9", config.ApiVersion);
        }
        finally
        {
            Environment.SetEnvironmentVariable(providerKey, oldProvider);
            Environment.SetEnvironmentVariable(apiKeyKey, oldApiKey);
            Environment.SetEnvironmentVariable(orgKey, oldOrg);
            Environment.SetEnvironmentVariable(baseUrlKey, oldBaseUrl);
            Environment.SetEnvironmentVariable(versionKey, oldVersion);
        }
    }

    [Theory]
    [InlineData("OpenAi", LLmProviders.OpenAi)]
    [InlineData("openai", LLmProviders.OpenAi)]
    [InlineData("Groq", LLmProviders.Groq)]
    [InlineData("UnknownProvider", LLmProviders.OpenAi)]
    [InlineData("", LLmProviders.OpenAi)]
    public void ApiFactory_ResolveProvider_HandlesKnownAndFallback(string input, LLmProviders expected)
    {
        var factory = new LLMTornadoApiFactory(new LLMTornadoConfigResolver());
        var actual = factory.ResolveProvider(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ApiFactory_Create_AppliesOverrides()
    {
        var factory = new LLMTornadoApiFactory(new LLMTornadoConfigResolver());

        var api = factory.Create(
            providerOverride: "Groq",
            apiKeyOverride: "secret",
            organizationOverride: "org-x",
            baseUrlOverride: "https://api.custom/{0}/{1}",
            apiVersionOverride: "v2");

        var auth = api.GetProviderAuthentication(LLmProviders.Groq);

        Assert.NotNull(auth);
        Assert.Equal(LLmProviders.Groq, auth!.Provider);
        Assert.Equal("secret", auth.ApiKey);
        Assert.Equal("org-x", auth.Organization);
        Assert.Equal("https://api.custom/{0}/{1}", api.ApiUrlFormat);
        Assert.Equal("v2", api.ApiVersion);
        Assert.Equal("NodeEditor.Plugins.LLMTornado/2.0", api.RequestSettings.UserAgent);
    }

    [Fact]
    public void StreamChatNode_Definition_DeclaresExpectedStreamSockets()
    {
        var definition = new NodeDiscoveryService().BuildDefinitionFromType(typeof(StreamChatNode));
        Assert.NotNull(definition);

        Assert.Contains(definition!.Outputs, s => s.Name == "Token" && !s.IsExecution);
        Assert.Contains(definition.Outputs, s => s.Name == "OnToken" && s.IsExecution);
        Assert.Contains(definition.Outputs, s => s.Name == "Completed" && s.IsExecution);
        Assert.NotNull(definition.StreamSockets);
        Assert.Contains(definition.StreamSockets!, s => s.ItemDataSocket == "Token" && s.OnItemExecSocket == "OnToken" && s.CompletedExecSocket == "Completed");
    }

    [Fact]
    public void StreamResponseNode_Definition_DeclaresExpectedStreamSockets()
    {
        var definition = new NodeDiscoveryService().BuildDefinitionFromType(typeof(StreamResponseNode));
        Assert.NotNull(definition);

        Assert.Contains(definition!.Outputs, s => s.Name == "Delta" && !s.IsExecution);
        Assert.Contains(definition.Outputs, s => s.Name == "OnDelta" && s.IsExecution);
        Assert.Contains(definition.Outputs, s => s.Name == "Completed" && s.IsExecution);
        Assert.NotNull(definition.StreamSockets);
        Assert.Contains(definition.StreamSockets!, s => s.ItemDataSocket == "Delta" && s.OnItemExecSocket == "OnDelta" && s.CompletedExecSocket == "Completed");
    }

    [Theory]
    [InlineData(typeof(SimpleChatNode))]
    [InlineData(typeof(CreateEmbeddingNode))]
    [InlineData(typeof(ListModelsNode))]
    [InlineData(typeof(CreateResponseNode))]
    [InlineData(typeof(StreamChatNode))]
    [InlineData(typeof(StreamResponseNode))]
    [InlineData(typeof(GenerateImageNode))]
    public async Task Nodes_WhenFactoryThrows_ReturnOkFalseAndError(Type nodeType)
    {
        var node = (NodeBase)Activator.CreateInstance(nodeType)!;

        var services = new ServiceCollection()
            .AddSingleton<ILLMTornadoApiFactory>(new ThrowingApiFactory())
            .BuildServiceProvider();

        var context = TestNodeExecutionContext.CreateFor(node, services);

        await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(context.Outputs.ContainsKey("Ok"));
        Assert.False(context.GetOutput<bool>("Ok"));
        Assert.Contains("factory failure", context.GetOutput<string>("Error"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Exit", context.TriggeredExecutionSockets);
    }

    [Fact]
    public async Task SimpleChatNode_WithMockedHttp_ReturnsSuccessOutputs()
    {
        var node = new SimpleChatNode();
        var services = new ServiceCollection()
            .AddSingleton<ILLMTornadoApiFactory>(new MockedHttpApiFactory())
            .BuildServiceProvider();

        var context = TestNodeExecutionContext.CreateFor(node, services);
        context.SetInput("EnableImageGeneration", true);
        await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(context.GetOutput<bool>("Ok"));
        Assert.Equal("mock-chat-response", context.GetOutput<string>("Response"));
        Assert.Equal(8, context.GetOutput<int>("TotalTokens"));
        Assert.Contains("Exit", context.TriggeredExecutionSockets);
    }

    [Fact]
    public async Task CreateEmbeddingNode_WithMockedHttp_ReturnsVector()
    {
        var node = new CreateEmbeddingNode();
        var services = new ServiceCollection()
            .AddSingleton<ILLMTornadoApiFactory>(new MockedHttpApiFactory())
            .BuildServiceProvider();

        var context = TestNodeExecutionContext.CreateFor(node, services);
        context.SetInput("EnableImageGeneration", true);
        await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(context.GetOutput<bool>("Ok"));
        var vector = context.GetOutput<float[]>("Vector");
        Assert.Equal(3, vector.Length);
        Assert.Equal(3, context.GetOutput<int>("Dimensions"));
        Assert.Equal(3, context.GetOutput<int>("TotalTokens"));
        Assert.Contains("Exit", context.TriggeredExecutionSockets);
    }

    [Fact]
    public async Task ListModelsNode_WithMockedHttp_ReturnsModelIds()
    {
        var node = new ListModelsNode();
        var services = new ServiceCollection()
            .AddSingleton<ILLMTornadoApiFactory>(new MockedHttpApiFactory())
            .BuildServiceProvider();

        var context = TestNodeExecutionContext.CreateFor(node, services);
        await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(context.GetOutput<bool>("Ok"));
        var ids = context.GetOutput<string[]>("ModelIds");
        Assert.Contains("gpt-4.1-mini", ids);
        Assert.Contains("text-embedding-3-small", ids);
        Assert.Equal(ids.Length, context.GetOutput<int>("Count"));
        Assert.Contains("Exit", context.TriggeredExecutionSockets);
    }

    [Fact]
    public async Task CreateResponseNode_WithMockedHttp_ReturnsSuccessOutputs()
    {
        var node = new CreateResponseNode();
        var services = new ServiceCollection()
            .AddSingleton<ILLMTornadoApiFactory>(new MockedHttpApiFactory())
            .BuildServiceProvider();

        var context = TestNodeExecutionContext.CreateFor(node, services);
        await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(context.GetOutput<bool>("Ok"));
        Assert.Equal("resp_1", context.GetOutput<string>("ResponseId"));
        Assert.Equal("mock response text", context.GetOutput<string>("OutputText"));
        Assert.Equal("Completed", context.GetOutput<string>("Status"));
        Assert.StartsWith("data:image/", context.GetOutput<string>("GeneratedImageReference"), StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(context.GetOutput<NodeImage?>("GeneratedImage"));
        Assert.Equal(7, context.GetOutput<int>("TotalTokens"));
        Assert.Contains("Exit", context.TriggeredExecutionSockets);
    }

    [Fact]
    public async Task StreamChatNode_WithMockedHttp_EmitsTokensAndCompletes()
    {
        var node = new StreamChatNode();
        var services = new ServiceCollection()
            .AddSingleton<ILLMTornadoApiFactory>(new MockedHttpApiFactory())
            .BuildServiceProvider();

        var context = TestNodeExecutionContext.CreateFor(node, services);
        await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(context.GetOutput<bool>("Ok"));
        Assert.Equal("mock stream", context.GetOutput<string>("FinalResponse"));
        Assert.Equal(5, context.GetOutput<int>("TotalTokens"));
        Assert.True(context.EmittedItems.TryGetValue("Token", out var tokens));
        Assert.Equal(new[] { "mock ", "stream" }, tokens!.Cast<string>().ToArray());
        Assert.Contains("Completed", context.TriggeredExecutionSockets);
    }

    [Fact]
    public async Task StreamResponseNode_WithMockedHttp_EmitsDeltasAndCompletes()
    {
        var node = new StreamResponseNode();
        var services = new ServiceCollection()
            .AddSingleton<ILLMTornadoApiFactory>(new MockedHttpApiFactory())
            .BuildServiceProvider();

        var context = TestNodeExecutionContext.CreateFor(node, services);
        await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(context.GetOutput<bool>("Ok"));
        Assert.Equal("Hello world", context.GetOutput<string>("FinalText"));
        Assert.StartsWith("data:image/", context.GetOutput<string>("GeneratedImageReference"), StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(context.GetOutput<NodeImage?>("GeneratedImage"));
        Assert.Equal(6, context.GetOutput<int>("TotalTokens"));
        Assert.True(context.EmittedItems.TryGetValue("Delta", out var deltas));
        Assert.Equal(new[] { "Hello", " world" }, deltas!.Cast<string>().ToArray());
        Assert.True(context.EmittedItems.TryGetValue("ImageDelta", out var imageDeltas));
        Assert.NotNull(imageDeltas!.FirstOrDefault());
        Assert.Contains("Completed", context.TriggeredExecutionSockets);
    }

    [Fact]
    public async Task GenerateImageNode_WithMockedHttp_ReturnsImageAndReference()
    {
        var node = new GenerateImageNode();
        var services = new ServiceCollection()
            .AddSingleton<ILLMTornadoApiFactory>(new MockedHttpApiFactory())
            .BuildServiceProvider();

        var context = TestNodeExecutionContext.CreateFor(node, services);
        await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(context.GetOutput<bool>("Ok"));
        var image = context.GetOutput<NodeImage?>("Image");
        Assert.NotNull(image);
        Assert.StartsWith("data:image/", image!.DataUrl, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("data:image/", context.GetOutput<string>("ImageReference"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(12, context.GetOutput<int>("TotalTokens"));
        Assert.Contains("Exit", context.TriggeredExecutionSockets);
    }

    [Fact]
    public async Task LoadImageNode_WithDataUrl_ReturnsImage()
    {
        var node = new LoadImageNode();
        var services = new ServiceCollection().BuildServiceProvider();

        var context = TestNodeExecutionContext.CreateFor(node, services);
        context.SetInput("ImagePath", "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/qz8AAAAASUVORK5CYII=");

        await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(context.GetOutput<bool>("Ok"));
        Assert.NotNull(context.GetOutput<NodeImage?>("Image"));
        Assert.Equal(string.Empty, context.GetOutput<string>("Error"));
        Assert.Contains("Exit", context.TriggeredExecutionSockets);
    }

    [Fact]
    public async Task LoadImageFromUrlNode_WithInvalidUrl_ReturnsError()
    {
        var node = new LoadImageFromUrlNode();
        var services = new ServiceCollection().BuildServiceProvider();

        var context = TestNodeExecutionContext.CreateFor(node, services);
        context.SetInput("ImageUrl", "not-a-url");

        await node.ExecuteAsync(context, CancellationToken.None);

        Assert.False(context.GetOutput<bool>("Ok"));
        Assert.NotNull(context.GetOutput<string>("Error"));
        Assert.Contains("absolute URL", context.GetOutput<string>("Error"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Exit", context.TriggeredExecutionSockets);
    }

    private sealed class ThrowingApiFactory : ILLMTornadoApiFactory
    {
        public TornadoApi Create(string? providerOverride = null, string? apiKeyOverride = null, string? organizationOverride = null, string? baseUrlOverride = null, string? apiVersionOverride = null)
            => throw new InvalidOperationException("factory failure");

        public LLmProviders ResolveProvider(string? providerName) => LLmProviders.OpenAi;
    }

        private sealed class MockedHttpApiFactory : ILLMTornadoApiFactory
        {
                private static readonly HttpClient SharedClient = new(new FakeLlmHttpMessageHandler())
                {
                        Timeout = TimeSpan.FromSeconds(5)
                };

                public TornadoApi Create(string? providerOverride = null, string? apiKeyOverride = null, string? organizationOverride = null, string? baseUrlOverride = null, string? apiVersionOverride = null)
                {
                        EndpointBase.OnHttpClientRequestedAsync = _ => Task.FromResult<HttpClient?>(SharedClient);
                        EndpointBase.OnHttpClientRequested = _ => SharedClient;

                        var api = new TornadoApi(LLmProviders.OpenAi, "test-key")
                        {
                                ApiUrlFormat = "https://api.openai.com/{0}/{1}"
                        };

                        return api;
                }

                public LLmProviders ResolveProvider(string? providerName) => LLmProviders.OpenAi;
        }

        private sealed class FakeLlmHttpMessageHandler : HttpMessageHandler
        {
                protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
                        var body = request.Content is null
                                ? string.Empty
                                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                        var isStream = body.Contains("\"stream\":true", StringComparison.OrdinalIgnoreCase);

                        if (request.Method == HttpMethod.Post && path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
                        {
                                return isStream ? CreateSse(ChatStreamSse) : CreateJson(ChatCompletionJson);
                        }

                        if (request.Method == HttpMethod.Post && path.EndsWith("/embeddings", StringComparison.OrdinalIgnoreCase))
                        {
                                return CreateJson(EmbeddingsJson);
                        }

                        if (request.Method == HttpMethod.Get && path.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
                        {
                                return CreateJson(ModelsJson);
                        }

                        if (request.Method == HttpMethod.Post && path.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
                        {
                                return isStream ? CreateSse(ResponseStreamSse) : CreateJson(ResponseJson);
                        }

                        if (request.Method == HttpMethod.Post && path.EndsWith("/images/generations", StringComparison.OrdinalIgnoreCase))
                        {
                            return CreateJson(ImageGenerationJson);
                        }

                        return new HttpResponseMessage(HttpStatusCode.NotFound)
                        {
                                Content = new StringContent("{}", Encoding.UTF8, "application/json")
                        };
                }

                private static HttpResponseMessage CreateJson(string json)
                {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                                Content = new StringContent(json, Encoding.UTF8, "application/json")
                        };
                }

                private static HttpResponseMessage CreateSse(string sse)
                {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                                Content = new StringContent(sse, Encoding.UTF8, "text/event-stream")
                        };
                }

                private const string ChatCompletionJson = """
                        {
                            "id": "chatcmpl_1",
                            "choices": [
                                {
                                    "index": 0,
                                    "message": {
                                        "role": "assistant",
                                        "content": "mock-chat-response"
                                    },
                                    "finish_reason": "stop"
                                }
                            ],
                            "usage": {
                                "prompt_tokens": 3,
                                "total_tokens": 8
                            }
                        }
                        """;

                private const string EmbeddingsJson = """
                        {
                            "data": [
                                {
                                    "object": "embedding",
                                    "embedding": [0.1, 0.2, 0.3],
                                    "index": 0
                                }
                            ],
                            "usage": {
                                "prompt_tokens": 3,
                                "total_tokens": 3
                            }
                        }
                        """;

                private const string ModelsJson = """
                        {
                            "data": [
                                { "id": "gpt-4.1-mini" },
                                { "id": "text-embedding-3-small" }
                            ]
                        }
                        """;

                private const string ResponseJson = """
                        {
                            "id": "resp_1",
                            "status": "completed",
                            "output": [
                                {
                                    "type": "image_generation_call",
                                    "id": "img_1",
                                    "status": "completed",
                                    "result": "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/qz8AAAAASUVORK5CYII="
                                },
                                {
                                    "type": "message",
                                    "id": "msg_1",
                                    "status": "completed",
                                    "role": "assistant",
                                    "content": [
                                        {
                                            "type": "output_text",
                                            "text": "mock response text",
                                            "annotations": []
                                        }
                                    ]
                                }
                            ],
                            "usage": {
                                "input_tokens": 3,
                                "output_tokens": 4,
                                "total_tokens": 7
                            }
                        }
                        """;

                private const string ChatStreamSse = """
                        data: {"id":"chatcmpl_stream_1","choices":[{"index":0,"delta":{"content":"mock "}}]}

                        data: {"id":"chatcmpl_stream_1","choices":[{"index":0,"delta":{"content":"stream"}}],"usage":{"prompt_tokens":1,"total_tokens":5}}

                        data: {"id":"chatcmpl_stream_1","choices":[{"index":0,"finish_reason":"stop"}]}

                        data: [DONE]

                        """;

                private const string ResponseStreamSse = """
                        event: response.output_text.delta
                        data: {"type":"response.output_text.delta","sequence_number":1,"content_index":0,"item_id":"msg_1","output_index":0,"delta":"Hello"}

                        event: response.image_generation_call.partial_image
                        data: {"type":"response.image_generation_call.partial_image","sequence_number":1,"item_id":"img_1","output_index":0,"partial_image_b64":"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/qz8AAAAASUVORK5CYII=","partial_image_index":0}

                        event: response.output_text.delta
                        data: {"type":"response.output_text.delta","sequence_number":2,"content_index":0,"item_id":"msg_1","output_index":0,"delta":" world"}

                        event: response.completed
                        data: {"type":"response.completed","sequence_number":3,"response":{"id":"resp_stream_1","status":"completed","output":[{"type":"image_generation_call","id":"img_1","status":"completed","result":"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/qz8AAAAASUVORK5CYII="},{"type":"message","id":"msg_1","status":"completed","role":"assistant","content":[{"type":"output_text","text":"Hello world","annotations":[]}]}],"usage":{"input_tokens":2,"output_tokens":4,"total_tokens":6}}}

                        data: [DONE]

                        """;

                private const string ImageGenerationJson = """
                        {
                            "created": 1730000000,
                            "data": [
                                {
                                    "b64_json": "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/qz8AAAAASUVORK5CYII="
                                }
                            ],
                            "usage": {
                                "input_tokens": 5,
                                "output_tokens": 7,
                                "total_tokens": 12
                            }
                        }
                        """;
        }

    private sealed class TestNodeExecutionContext : INodeExecutionContext
    {
        private readonly Dictionary<string, object?> _inputs;
        private readonly NodeRuntimeStorage _storage = new();

        public NodeData Node { get; }
        public IServiceProvider Services { get; }
        public CancellationToken CancellationToken { get; }
        public ExecutionEventBus EventBus => _storage.EventBus;
        public INodeRuntimeStorage RuntimeStorage => _storage;

        public Dictionary<string, object?> Outputs { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, List<object?>> EmittedItems { get; } = new(StringComparer.Ordinal);
        public List<string> TriggeredExecutionSockets { get; } = [];

        private TestNodeExecutionContext(NodeData node, IServiceProvider services, Dictionary<string, object?> inputs, CancellationToken token)
        {
            Node = node;
            Services = services;
            _inputs = inputs;
            CancellationToken = token;
        }

        public static TestNodeExecutionContext CreateFor(NodeBase node, IServiceProvider services, CancellationToken token = default)
        {
            var definition = new NodeDiscoveryService().BuildDefinitionFromType(node.GetType())!;
            var nodeData = definition.Factory();

            var inputs = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var input in nodeData.Inputs.Where(s => !s.IsExecution))
            {
                inputs[input.Name] = input.Value?.ToObject<object?>() switch
                {
                    null => input.TypeName == typeof(string).FullName ? string.Empty : null,
                    var value => value
                };
            }

            return new TestNodeExecutionContext(nodeData, services, inputs, token);
        }

        public T GetInput<T>(string socketName)
        {
            if (!_inputs.TryGetValue(socketName, out var value) || value is null)
            {
                return default!;
            }

            return Cast<T>(value);
        }

        public object? GetInput(string socketName)
        {
            _inputs.TryGetValue(socketName, out var value);
            return value;
        }

        public void SetInput(string socketName, object? value)
        {
            _inputs[socketName] = value;
        }

        public bool TryGetInput<T>(string socketName, out T value)
        {
            if (_inputs.TryGetValue(socketName, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }

            value = default!;
            return false;
        }

        public void SetOutput<T>(string socketName, T value)
        {
            Outputs[socketName] = value;
        }

        public void SetOutput(string socketName, object? value)
        {
            Outputs[socketName] = value;
        }

        public Task TriggerAsync(string executionOutputName)
        {
            TriggeredExecutionSockets.Add(executionOutputName);
            return Task.CompletedTask;
        }

        public Task TriggerScopedAsync(string executionOutputName, INodeRuntimeStorage scope)
        {
            TriggeredExecutionSockets.Add(executionOutputName);
            return Task.CompletedTask;
        }

        public Task EmitAsync<T>(string streamItemSocket, T item)
        {
            return EmitAsync(streamItemSocket, (object?)item);
        }

        public Task EmitAsync(string streamItemSocket, object? item)
        {
            if (!EmittedItems.TryGetValue(streamItemSocket, out var list))
            {
                list = [];
                EmittedItems[streamItemSocket] = list;
            }

            list.Add(item);
            Outputs[streamItemSocket] = item;
            return Task.CompletedTask;
        }

        public object? GetVariable(string key) => _storage.GetVariable(key);

        public void SetVariable(string key, object? value) => _storage.SetVariable(key, value);

        public void EmitFeedback(string message, ExecutionFeedbackType type = ExecutionFeedbackType.DebugPrint, object? tag = null)
        {
        }

        public T GetOutput<T>(string socketName)
        {
            if (!Outputs.TryGetValue(socketName, out var value) || value is null)
            {
                return default!;
            }

            return Cast<T>(value);
        }

        private static T Cast<T>(object? value)
        {
            if (value is T typed)
            {
                return typed;
            }

            if (value is null)
            {
                return default!;
            }

            if (value is System.Text.Json.JsonElement json)
            {
                return System.Text.Json.JsonSerializer.Deserialize<T>(json.GetRawText())!;
            }

            return (T)Convert.ChangeType(value, typeof(T));
        }
    }
}