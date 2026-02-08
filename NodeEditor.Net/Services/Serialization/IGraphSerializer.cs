using NodeEditor.Net.Models;
using NodeEditor.Net.Services;

namespace NodeEditor.Net.Services.Serialization;

public interface IGraphSerializer
{
    GraphData ExportToGraphData(INodeEditorState state);
    void Import(INodeEditorState state, GraphData graphData);

    GraphDto Export(INodeEditorState state);
    GraphImportResult Import(INodeEditorState state, GraphDto dto);

    string SerializeGraphData(GraphData graphData);
    GraphData DeserializeToGraphData(string json);

    string Serialize(GraphDto dto);
    GraphDto Deserialize(string json);
}
