namespace NodeEditor.Net.Services.Serialization;

/// <summary>
/// Maps old method-signature-based DefinitionIds (from the INodeContext/[Node] attribute system)
/// to new class-name-based DefinitionIds (from the NodeBase/NodeBuilder system).
/// Applied during graph deserialization so that old saved graphs resolve to new definitions.
/// </summary>
public static class DefinitionIdMigration
{
    private const string Old = "NodeEditor.Net.Services.Execution.StandardNodeContext";
    private const string Exec = "NodeEditor.Net.Services.Execution.ExecutionPath";
    private const string SList = "NodeEditor.Net.Models.SerializableList";
    private const string NImage = "NodeEditor.Net.Models.NodeImage";
    private const string Ns = "NodeEditor.Net.Services.Execution.StandardNodes";

    private static readonly Dictionary<string, string> Map = new(StringComparer.Ordinal)
    {
        // ── Helpers ──
        [$"{Old}.Start({Exec})"] = $"{Ns}.StartNode",
        [$"{Old}.Marker({Exec},{Exec})"] = $"{Ns}.MarkerNode",
        [$"{Old}.Consume({Exec},System.Object,{Exec})"] = $"{Ns}.ConsumeNode",
        [$"{Old}.Delay({Exec},System.Int32,System.Threading.CancellationToken,{Exec})"] = $"{Ns}.DelayNode",

        // ── Conditions / Loops ──
        [$"{Old}.Branch({Exec},System.Boolean,{Exec},{Exec})"] = $"{Ns}.BranchNode",
        [$"{Old}.WhileLoop(System.Boolean,{Exec},{Exec})"] = $"{Ns}.WhileLoopNode",
        [$"{Old}.ForLoop(System.Int32,{Exec},{Exec},System.Int32)"] = $"{Ns}.ForLoopNode",
        [$"{Old}.ForEachLoop({SList},{Exec},{Exec},System.Object)"] = $"{Ns}.ForEachLoopNode",
        [$"{Old}.ForLoopStep(System.Int32,System.Int32,System.Int32,{Exec},{Exec},System.Int32)"] = $"{Ns}.ForLoopStepNode",
        [$"{Old}.DoWhileLoop(System.Boolean,{Exec},{Exec})"] = $"{Ns}.DoWhileLoopNode",
        [$"{Old}.RepeatUntil(System.Boolean,{Exec},{Exec})"] = $"{Ns}.RepeatUntilNode",

        // ── Debug ──
        [$"{Old}.DebugPrint({Exec},System.String,System.Object,{Exec})"] = $"{Ns}.DebugPrintNode",
        [$"{Old}.PrintValue(System.String,System.Object,System.Object)"] = $"{Ns}.PrintValueNode",
        [$"{Old}.DebugWarning({Exec},System.String,{Exec})"] = $"{Ns}.DebugWarningNode",
        [$"{Old}.DebugError({Exec},System.String,{Exec})"] = $"{Ns}.DebugErrorNode",

        // ── Numbers ──
        [$"{Old}.NumAbs(System.Double,System.Double)"] = "Abs",
        [$"{Old}.NumMin(System.Double,System.Double,System.Double)"] = "Min",
        [$"{Old}.NumMax(System.Double,System.Double,System.Double)"] = "Max",
        [$"{Old}.NumMod(System.Double,System.Double,System.Double)"] = "Mod",
        [$"{Old}.NumRound(System.Double,System.Int32,System.Double)"] = "Round",
        [$"{Old}.NumFloor(System.Double,System.Double)"] = "Floor",
        [$"{Old}.NumCeiling(System.Double,System.Double)"] = "Ceiling",
        [$"{Old}.NumClamp(System.Double,System.Double,System.Double,System.Double)"] = "Clamp",
        [$"{Old}.NumRandomRange(System.Double,System.Double,System.Double)"] = "Random Range",
        [$"{Old}.NumSign(System.Double,System.Int32)"] = "Sign",

        // ── Strings ──
        [$"{Old}.StringConcat(System.String,System.String,System.String)"] = "String Concat",
        [$"{Old}.StringLength(System.String,System.Int32)"] = "String Length",
        [$"{Old}.StringSubstring(System.String,System.Int32,System.Int32,System.String)"] = "Substring",
        [$"{Old}.StringReplace(System.String,System.String,System.String,System.String)"] = "Replace",
        [$"{Old}.StringToUpper(System.String,System.String)"] = "To Upper",
        [$"{Old}.StringToLower(System.String,System.String)"] = "To Lower",
        [$"{Old}.StringTrim(System.String,System.String)"] = "Trim",
        [$"{Old}.StringContains(System.String,System.String,System.Boolean)"] = "Contains",
        [$"{Old}.StringStartsWith(System.String,System.String,System.Boolean)"] = "Starts With",
        [$"{Old}.StringEndsWith(System.String,System.String,System.Boolean)"] = "Ends With",
        [$"{Old}.StringSplit(System.String,System.String,{SList})"] = "Split",
        [$"{Old}.StringJoin({SList},System.String,System.String)"] = "Join",

        // ── Lists ──
        [$"{Old}.ListCreate({SList})"] = "List Create",
        [$"{Old}.ListAdd({SList},System.Object,{SList})"] = "List Add",
        [$"{Old}.ListInsert({SList},System.Int32,System.Object,System.Boolean)"] = "List Insert",
        [$"{Old}.ListRemoveAt({SList},System.Int32,System.Boolean,System.Object)"] = "List Remove At",
        [$"{Old}.ListRemoveValue({SList},System.Object,System.Boolean)"] = "List Remove Value",
        [$"{Old}.ListClear({SList},{SList})"] = "List Clear",
        [$"{Old}.ListContains({SList},System.Object,System.Boolean)"] = "List Contains",
        [$"{Old}.ListIndexOf({SList},System.Object,System.Int32)"] = "List Index Of",
        [$"{Old}.ListCount({SList},System.Int32)"] = "List Count",
        [$"{Old}.ListGet({SList},System.Int32,System.Object,System.Boolean)"] = "List Get",
        [$"{Old}.ListSet({SList},System.Int32,System.Object,System.Boolean)"] = "List Set",
        [$"{Old}.ListSlice({SList},System.Int32,System.Int32,{SList})"] = "List Slice",

        // ── Plugin: Template ──
        ["NodeEditor.Plugins.Template.TemplatePluginContext.Echo(System.String,System.String)"] = "NodeEditor.Plugins.Template.EchoNode",

        // ── Plugin: TestA ──
        ["NodeEditor.Plugins.TestA.TestAPluginContext.Echo(System.String,System.String)"] = "NodeEditor.Plugins.TestA.EchoStringNode",
        [$"NodeEditor.Plugins.TestA.TestAPluginContext.Ping({Exec})"] = "NodeEditor.Plugins.TestA.PingNode",
        [$"NodeEditor.Plugins.TestA.TestAPluginContext.LoadImage(System.String,{NImage},{Exec})"] = "NodeEditor.Plugins.TestA.LoadImageNode",

        // ── Plugin: TestB ──
        ["NodeEditor.Plugins.TestB.TestBPluginContext.Add(System.Int32,System.Int32,System.Int32)"] = "NodeEditor.Plugins.TestB.AddIntsNode",
        ["NodeEditor.Plugins.TestB.TestBPluginContext.ToUpper(System.String,System.String)"] = "NodeEditor.Plugins.TestB.ToUpperNode",
    };

    /// <summary>
    /// Returns the new DefinitionId for an old-format ID, or the input unchanged
    /// if no migration is needed.
    /// </summary>
    public static string Migrate(string definitionId)
        => Map.TryGetValue(definitionId, out var newId) ? newId : definitionId;
}
