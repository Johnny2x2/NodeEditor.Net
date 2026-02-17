using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Blazor.Tests;

public sealed class NodeSuiteTests
{
    private static NodeRegistryService CreateRegistry()
    {
        var registry = new NodeRegistryService(new NodeDiscoveryService());
        registry.EnsureInitialized();
        return registry;
    }

    private static NodeExecutionService CreateService(NodeRegistryService registry)
        => new(new ExecutionPlanner(), registry, new MinimalServiceProvider());

    private static NodeData FromDef(NodeRegistryService reg, string name, string id, params (string socket, object value)[] overrides)
    {
        var def = reg.Definitions.First(d => d.Name == name && (d.NodeType is not null || d.InlineExecutor is not null));
        var node = def.Factory() with { Id = id };
        if (overrides.Length == 0) return node;
        var inputs = node.Inputs.Select(s =>
        {
            var o = overrides.FirstOrDefault(x => x.socket == s.Name);
            return o != default ? s with { Value = SocketValue.FromObject(o.value) } : s;
        }).ToArray();
        return node with { Inputs = inputs };
    }

    /// <summary>Execute a pure data pipeline: start → consume (pulling from data chain)</summary>
    private static async Task<NodeRuntimeStorage> ExecuteDataPipeline(
        NodeRegistryService registry, List<NodeData> nodes, List<ConnectionData> connections)
    {
        var service = CreateService(registry);
        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);
        return context;
    }

    // ── Number Nodes ──

    [Fact]
    public async Task Abs_NegativeReturnsPositive()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var abs = FromDef(reg, "Abs", "abs", ("Value", -7.0));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, abs, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("abs", "Result", "consume", "Value") });

        Assert.Equal(7.0, ctx.GetSocketValue("abs", "Result"));
    }

    [Fact]
    public async Task Min_ReturnsSmallerValue()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var min = FromDef(reg, "Min", "min", ("A", 3.0), ("B", 7.0));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, min, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("min", "Result", "consume", "Value") });

        Assert.Equal(3.0, ctx.GetSocketValue("min", "Result"));
    }

    [Fact]
    public async Task Max_ReturnsLargerValue()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var max = FromDef(reg, "Max", "max", ("A", 3.0), ("B", 7.0));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, max, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("max", "Result", "consume", "Value") });

        Assert.Equal(7.0, ctx.GetSocketValue("max", "Result"));
    }

    [Fact]
    public async Task Mod_ReturnsRemainder()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var mod = FromDef(reg, "Mod", "mod", ("A", 7.0), ("B", 3.0));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, mod, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("mod", "Result", "consume", "Value") });

        Assert.Equal(1.0, ctx.GetSocketValue("mod", "Result"));
    }

    [Fact]
    public async Task Round_RoundsToNearest()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var round = FromDef(reg, "Round", "round", ("Value", 3.6));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, round, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("round", "Result", "consume", "Value") });

        Assert.Equal(4.0, ctx.GetSocketValue("round", "Result"));
    }

    [Fact]
    public async Task Floor_RoundsDown()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var floor = FromDef(reg, "Floor", "floor", ("Value", 3.9));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, floor, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("floor", "Result", "consume", "Value") });

        Assert.Equal(3.0, ctx.GetSocketValue("floor", "Result"));
    }

    [Fact]
    public async Task Ceiling_RoundsUp()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var ceil = FromDef(reg, "Ceiling", "ceil", ("Value", 3.1));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, ceil, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("ceil", "Result", "consume", "Value") });

        Assert.Equal(4.0, ctx.GetSocketValue("ceil", "Result"));
    }

    [Fact]
    public async Task Clamp_ClampsToRange()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var clamp = FromDef(reg, "Clamp", "clamp", ("Value", 15.0), ("Min", 0.0), ("Max", 10.0));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, clamp, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("clamp", "Result", "consume", "Value") });

        Assert.Equal(10.0, ctx.GetSocketValue("clamp", "Result"));
    }

    [Fact]
    public async Task Sign_ReturnsNegativeOne()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var sign = FromDef(reg, "Sign", "sign", ("Value", -5.0));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, sign, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("sign", "Result", "consume", "Value") });

        Assert.Equal(-1, ctx.GetSocketValue("sign", "Result"));
    }

    [Fact]
    public async Task RandomRange_ProducesValueInRange()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var rand = FromDef(reg, "Random Range", "rand", ("Min", 10), ("Max", 20));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, rand, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("rand", "Result", "consume", "Value") });

        var result = (int)ctx.GetSocketValue("rand", "Result")!;
        Assert.InRange(result, 10, 19);
    }

    // ── String Nodes ──

    [Fact]
    public async Task StringConcat_ConcatenatesTwoStrings()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var concat = FromDef(reg, "String Concat", "concat", ("A", "Hello "), ("B", "World"));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, concat, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("concat", "Result", "consume", "Value") });

        Assert.Equal("Hello World", ctx.GetSocketValue("concat", "Result"));
    }

    [Fact]
    public async Task StringLength_ReturnsCorrectLength()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var len = FromDef(reg, "String Length", "len", ("Value", "abc"));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, len, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("len", "Result", "consume", "Value") });

        Assert.Equal(3, ctx.GetSocketValue("len", "Result"));
    }

    [Fact]
    public async Task StringConcat_PipeToLength()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var concat = FromDef(reg, "String Concat", "concat", ("A", "Hello "), ("B", "World"));
        var len = FromDef(reg, "String Length", "len");
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, concat, len, consume },
            new()
            {
                TestConnections.Exec("start", "Exit", "consume", "Enter"),
                TestConnections.Data("concat", "Result", "len", "Value"),
                TestConnections.Data("len", "Result", "consume", "Value")
            });

        Assert.Equal(11, ctx.GetSocketValue("len", "Result"));
    }

    [Fact]
    public async Task ToUpper_ConvertsToUppercase()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var upper = FromDef(reg, "To Upper", "upper", ("Value", "hello"));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, upper, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("upper", "Result", "consume", "Value") });

        Assert.Equal("HELLO", ctx.GetSocketValue("upper", "Result"));
    }

    [Fact]
    public async Task ToLower_ConvertsToLowercase()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var lower = FromDef(reg, "To Lower", "lower", ("Value", "HELLO"));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, lower, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("lower", "Result", "consume", "Value") });

        Assert.Equal("hello", ctx.GetSocketValue("lower", "Result"));
    }

    [Fact]
    public async Task Trim_RemovesWhitespace()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var trim = FromDef(reg, "Trim", "trim", ("Value", "  hello  "));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, trim, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("trim", "Result", "consume", "Value") });

        Assert.Equal("hello", ctx.GetSocketValue("trim", "Result"));
    }

    [Fact]
    public async Task Contains_FindsSubstring()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var contains = FromDef(reg, "Contains", "contains", ("Value", "hello world"), ("Search", "world"));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, contains, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("contains", "Result", "consume", "Value") });

        Assert.Equal(true, ctx.GetSocketValue("contains", "Result"));
    }

    [Fact]
    public async Task Replace_ReplacesSubstring()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var repl = FromDef(reg, "Replace", "repl", ("Value", "hello world"), ("Old", "world"), ("New", "earth"));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, repl, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("repl", "Result", "consume", "Value") });

        Assert.Equal("hello earth", ctx.GetSocketValue("repl", "Result"));
    }

    [Fact]
    public async Task Substring_ExtractsCorrectly()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var sub = FromDef(reg, "Substring", "sub", ("Value", "hello world"), ("Start", 6), ("Length", 5));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, sub, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("sub", "Result", "consume", "Value") });

        Assert.Equal("world", ctx.GetSocketValue("sub", "Result"));
    }

    [Fact]
    public async Task StartsWith_ChecksPrefix()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var sw = FromDef(reg, "Starts With", "sw", ("Value", "hello"), ("Prefix", "hel"));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, sw, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("sw", "Result", "consume", "Value") });

        Assert.Equal(true, ctx.GetSocketValue("sw", "Result"));
    }

    [Fact]
    public async Task EndsWith_ChecksSuffix()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var ew = FromDef(reg, "Ends With", "ew", ("Value", "hello"), ("Suffix", "llo"));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, ew, consume },
            new() { TestConnections.Exec("start", "Exit", "consume", "Enter"), TestConnections.Data("ew", "Result", "consume", "Value") });

        Assert.Equal(true, ctx.GetSocketValue("ew", "Result"));
    }

    [Fact]
    public async Task SplitAndJoin_Roundtrip()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var split = FromDef(reg, "Split", "split", ("Value", "a,b,c"), ("Delimiter", ","));
        var join = FromDef(reg, "Join", "join", ("Separator", "-"));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, split, join, consume },
            new()
            {
                TestConnections.Exec("start", "Exit", "consume", "Enter"),
                TestConnections.Data("split", "Result", "join", "List"),
                TestConnections.Data("join", "Result", "consume", "Value")
            });

        Assert.Equal("a-b-c", ctx.GetSocketValue("join", "Result"));
    }

    // ── List Nodes ──

    [Fact]
    public async Task ListCreate_ReturnsEmptyList()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var create = FromDef(reg, "List Create", "create");
        var count = FromDef(reg, "List Count", "count");
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, create, count, consume },
            new()
            {
                TestConnections.Exec("start", "Exit", "consume", "Enter"),
                TestConnections.Data("create", "Result", "count", "List"),
                TestConnections.Data("count", "Result", "consume", "Value")
            });

        Assert.Equal(0, ctx.GetSocketValue("count", "Result"));
    }

    [Fact]
    public async Task ListAdd_IncreasesCount()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var create = FromDef(reg, "List Create", "create");
        var add = FromDef(reg, "List Add", "add", ("Item", (object)"hello"));
        var count = FromDef(reg, "List Count", "count");
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, create, add, count, consume },
            new()
            {
                TestConnections.Exec("start", "Exit", "consume", "Enter"),
                TestConnections.Data("create", "Result", "add", "List"),
                TestConnections.Data("add", "Result", "count", "List"),
                TestConnections.Data("count", "Result", "consume", "Value")
            });

        Assert.Equal(1, ctx.GetSocketValue("count", "Result"));
    }

    [Fact]
    public async Task ListGetAndSet_WorkCorrectly()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var create = FromDef(reg, "List Create", "create");
        var add1 = FromDef(reg, "List Add", "add1", ("Item", (object)"A"));
        var add2 = FromDef(reg, "List Add", "add2", ("Item", (object)"B"));
        var set = FromDef(reg, "List Set", "set", ("Index", 0), ("Value", (object)"X"));
        var get = FromDef(reg, "List Get", "get", ("Index", 0));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, create, add1, add2, set, get, consume },
            new()
            {
                TestConnections.Exec("start", "Exit", "consume", "Enter"),
                TestConnections.Data("create", "Result", "add1", "List"),
                TestConnections.Data("add1", "Result", "add2", "List"),
                TestConnections.Data("add2", "Result", "set", "List"),
                TestConnections.Data("set", "Result", "get", "List"),
                TestConnections.Data("get", "Result", "consume", "Value")
            });

        Assert.Equal("X", ctx.GetSocketValue("get", "Result"));
    }

    [Fact]
    public async Task ListContains_FindsItem()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var create = FromDef(reg, "List Create", "create");
        var add = FromDef(reg, "List Add", "add", ("Item", (object)"needle"));
        var contains = FromDef(reg, "List Contains", "contains", ("Value", (object)"needle"));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, create, add, contains, consume },
            new()
            {
                TestConnections.Exec("start", "Exit", "consume", "Enter"),
                TestConnections.Data("create", "Result", "add", "List"),
                TestConnections.Data("add", "Result", "contains", "List"),
                TestConnections.Data("contains", "Result", "consume", "Value")
            });

        Assert.Equal(true, ctx.GetSocketValue("contains", "Result"));
    }

    [Fact]
    public async Task ListSlice_ReturnsSubList()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var create = FromDef(reg, "List Create", "create");
        var add1 = FromDef(reg, "List Add", "add1", ("Item", (object)"A"));
        var add2 = FromDef(reg, "List Add", "add2", ("Item", (object)"B"));
        var add3 = FromDef(reg, "List Add", "add3", ("Item", (object)"C"));
        var add4 = FromDef(reg, "List Add", "add4", ("Item", (object)"D"));
        var slice = FromDef(reg, "List Slice", "slice", ("Start", 1), ("Count", 2));
        var count = FromDef(reg, "List Count", "count");
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, create, add1, add2, add3, add4, slice, count, consume },
            new()
            {
                TestConnections.Exec("start", "Exit", "consume", "Enter"),
                TestConnections.Data("create", "Result", "add1", "List"),
                TestConnections.Data("add1", "Result", "add2", "List"),
                TestConnections.Data("add2", "Result", "add3", "List"),
                TestConnections.Data("add3", "Result", "add4", "List"),
                TestConnections.Data("add4", "Result", "slice", "List"),
                TestConnections.Data("slice", "Result", "count", "List"),
                TestConnections.Data("count", "Result", "consume", "Value")
            });

        Assert.Equal(2, ctx.GetSocketValue("count", "Result"));
    }

    [Fact]
    public async Task ListIndexOf_ReturnsCorrectIndex()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var create = FromDef(reg, "List Create", "create");
        var add1 = FromDef(reg, "List Add", "add1", ("Item", (object)"A"));
        var add2 = FromDef(reg, "List Add", "add2", ("Item", (object)"B"));
        var indexOf = FromDef(reg, "List Index Of", "indexOf", ("Value", (object)"B"));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, create, add1, add2, indexOf, consume },
            new()
            {
                TestConnections.Exec("start", "Exit", "consume", "Enter"),
                TestConnections.Data("create", "Result", "add1", "List"),
                TestConnections.Data("add1", "Result", "add2", "List"),
                TestConnections.Data("add2", "Result", "indexOf", "List"),
                TestConnections.Data("indexOf", "Result", "consume", "Value")
            });

        Assert.Equal(1, ctx.GetSocketValue("indexOf", "Result"));
    }

    [Fact]
    public async Task ListRemoveAt_RemovesCorrectItem()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var create = FromDef(reg, "List Create", "create");
        var add1 = FromDef(reg, "List Add", "add1", ("Item", (object)"A"));
        var add2 = FromDef(reg, "List Add", "add2", ("Item", (object)"B"));
        var add3 = FromDef(reg, "List Add", "add3", ("Item", (object)"C"));
        var removeAt = FromDef(reg, "List Remove At", "removeAt", ("Index", 1));
        var count = FromDef(reg, "List Count", "count");
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, create, add1, add2, add3, removeAt, count, consume },
            new()
            {
                TestConnections.Exec("start", "Exit", "consume", "Enter"),
                TestConnections.Data("create", "Result", "add1", "List"),
                TestConnections.Data("add1", "Result", "add2", "List"),
                TestConnections.Data("add2", "Result", "add3", "List"),
                TestConnections.Data("add3", "Result", "removeAt", "List"),
                TestConnections.Data("removeAt", "Result", "count", "List"),
                TestConnections.Data("count", "Result", "consume", "Value")
            });

        Assert.Equal(2, ctx.GetSocketValue("count", "Result"));
    }

    [Fact]
    public async Task ListClear_ReturnsEmptyList()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var create = FromDef(reg, "List Create", "create");
        var add = FromDef(reg, "List Add", "add", ("Item", (object)"X"));
        var clear = FromDef(reg, "List Clear", "clear");
        var count = FromDef(reg, "List Count", "count");
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, create, add, clear, count, consume },
            new()
            {
                TestConnections.Exec("start", "Exit", "consume", "Enter"),
                TestConnections.Data("create", "Result", "add", "List"),
                TestConnections.Data("add", "Result", "clear", "List"),
                TestConnections.Data("clear", "Result", "count", "List"),
                TestConnections.Data("count", "Result", "consume", "Value")
            });

        Assert.Equal(0, ctx.GetSocketValue("count", "Result"));
    }

    [Fact]
    public async Task ListInsert_InsertsAtCorrectPosition()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var create = FromDef(reg, "List Create", "create");
        var add1 = FromDef(reg, "List Add", "add1", ("Item", (object)"A"));
        var add2 = FromDef(reg, "List Add", "add2", ("Item", (object)"C"));
        var insert = FromDef(reg, "List Insert", "insert", ("Index", 1), ("Item", (object)"B"));
        var get = FromDef(reg, "List Get", "get", ("Index", 1));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, create, add1, add2, insert, get, consume },
            new()
            {
                TestConnections.Exec("start", "Exit", "consume", "Enter"),
                TestConnections.Data("create", "Result", "add1", "List"),
                TestConnections.Data("add1", "Result", "add2", "List"),
                TestConnections.Data("add2", "Result", "insert", "List"),
                TestConnections.Data("insert", "Result", "get", "List"),
                TestConnections.Data("get", "Result", "consume", "Value")
            });

        Assert.Equal("B", ctx.GetSocketValue("get", "Result"));
    }

    // ── PrintValue (data-only debug) ──

    [Fact]
    public async Task PrintValue_PassesThroughValue()
    {
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var abs = FromDef(reg, "Abs", "abs", ("Value", -42.0));
        var print = FromDef(reg, "Print Value", "print");
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, abs, print, consume },
            new()
            {
                TestConnections.Exec("start", "Exit", "consume", "Enter"),
                TestConnections.Data("abs", "Result", "print", "Value"),
                TestConnections.Data("print", "PassThrough", "consume", "Value")
            });

        Assert.Equal(42.0, ctx.GetSocketValue("print", "PassThrough"));
    }

    // ── Cross-category pipeline ──

    [Fact]
    public async Task CrossCategory_NumberToStringPipeline()
    {
        // Abs(-42) → value is 42.0 (double), then consume it
        var reg = CreateRegistry();
        var start = FromDef(reg, "Start", "start");
        var abs = FromDef(reg, "Abs", "abs", ("Value", -42.0));
        var consume = FromDef(reg, "Consume", "consume");

        var ctx = await ExecuteDataPipeline(reg, new() { start, abs, consume },
            new()
            {
                TestConnections.Exec("start", "Exit", "consume", "Enter"),
                TestConnections.Data("abs", "Result", "consume", "Value")
            });

        Assert.Equal(42.0, ctx.GetSocketValue("abs", "Result"));
    }

    // ── Discovery/Registration ──

    [Fact]
    public void Registry_ContainsAllInlineNumberNodes()
    {
        var reg = CreateRegistry();
        var names = new[] { "Abs", "Min", "Max", "Mod", "Round", "Floor", "Ceiling", "Clamp", "Random Range", "Sign" };
        foreach (var name in names)
            Assert.Contains(reg.Definitions, d => d.Name == name);
    }

    [Fact]
    public void Registry_ContainsAllInlineStringNodes()
    {
        var reg = CreateRegistry();
        var names = new[] { "String Concat", "String Length", "Substring", "Replace", "To Upper", "To Lower", "Trim", "Contains", "Starts With", "Ends With", "Split", "Join" };
        foreach (var name in names)
            Assert.Contains(reg.Definitions, d => d.Name == name);
    }

    [Fact]
    public void Registry_ContainsAllInlineListNodes()
    {
        var reg = CreateRegistry();
        var names = new[] { "List Create", "List Add", "List Insert", "List Remove At", "List Remove Value", "List Clear", "List Contains", "List Index Of", "List Count", "List Get", "List Set", "List Slice" };
        foreach (var name in names)
            Assert.Contains(reg.Definitions, d => d.Name == name);
    }

    [Fact]
    public void Registry_ContainsAllNodeBaseNodes()
    {
        var reg = CreateRegistry();
        var names = new[] { "Start", "Branch", "Marker", "Consume", "Delay", "For Loop", "For Loop Step", "ForEach Loop", "While Loop", "Do While Loop", "Repeat Until", "Debug Print", "Print Value", "Debug Warning", "Debug Error" };
        foreach (var name in names)
            Assert.Contains(reg.Definitions, d => d.Name == name);
    }

    [Fact]
    public void InlineDefinitions_HaveInlineExecutor()
    {
        var reg = CreateRegistry();
        var absDef = reg.Definitions.First(d => d.Name == "Abs" && d.InlineExecutor is not null);
        Assert.NotNull(absDef.InlineExecutor);
        Assert.Null(absDef.NodeType);
    }

    [Fact]
    public void NodeBaseDefinitions_HaveNodeType()
    {
        var reg = CreateRegistry();
        var startDef = reg.Definitions.First(d => d.Name == "Start" && d.NodeType is not null);
        Assert.NotNull(startDef.NodeType);
        Assert.Null(startDef.InlineExecutor);
    }
}
