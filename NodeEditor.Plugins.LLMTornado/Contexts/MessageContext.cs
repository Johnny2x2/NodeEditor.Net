using LlmTornado.Chat;
using LlmTornado.Code;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Plugins.LLMTornado.Contexts;

public sealed class MessageContext : INodeContext
{
    [Node("Create Message",
        category: "LLM/Messages",
        description: "Create a structured chat message with role and content",
        isCallable: false)]
    public void CreateMessage(
        string Role,
        string Content,
        out ChatMessage Message)
    {
        var role = Role?.ToLowerInvariant() switch
        {
            "system" => ChatMessageRoles.System,
            "assistant" => ChatMessageRoles.Assistant,
            _ => ChatMessageRoles.User
        };
        Message = new ChatMessage(role, Content ?? string.Empty);
    }

    [Node("Conversation Manager",
        category: "LLM/Messages",
        description: "Manage multi-turn conversation history",
        isCallable: false)]
    public void ManageConversation(
        ChatMessage[]? ExistingHistory,
        ChatMessage NewMessage,
        int MaxHistoryLength,
        out ChatMessage[] UpdatedHistory,
        out int MessageCount)
    {
        var history = ExistingHistory?.ToList() ?? new List<ChatMessage>();

        if (NewMessage is not null)
        {
            history.Add(NewMessage);
        }

        var maxLen = MaxHistoryLength > 0 ? MaxHistoryLength : 50;
        if (history.Count > maxLen)
        {
            history = history.Skip(history.Count - maxLen).ToList();
        }

        UpdatedHistory = history.ToArray();
        MessageCount = history.Count;
    }

    [Node("Append Message",
        category: "LLM/Messages",
        description: "Append a text message to an existing conversation",
        isCallable: false)]
    public void AppendMessage(
        ChatMessage[] History,
        string Role,
        string Content,
        out ChatMessage[] UpdatedHistory)
    {
        var role = Role?.ToLowerInvariant() switch
        {
            "system" => ChatMessageRoles.System,
            "assistant" => ChatMessageRoles.Assistant,
            _ => ChatMessageRoles.User
        };
        var list = History?.ToList() ?? new List<ChatMessage>();
        list.Add(new ChatMessage(role, Content ?? string.Empty));
        UpdatedHistory = list.ToArray();
    }

    [Node("Extract Response",
        category: "LLM/Messages",
        description: "Extract the text content from a ChatMessage",
        isCallable: false)]
    public void ExtractResponse(
        ChatMessage Message,
        out string Content,
        out string Role)
    {
        Content = Message?.Content ?? string.Empty;
        Role = Message?.Role?.ToString() ?? string.Empty;
    }
}
