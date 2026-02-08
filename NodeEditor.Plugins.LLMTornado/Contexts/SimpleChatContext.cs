using LlmTornado;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Registry;
using NodeEditor.Plugins.LLMTornado.Configuration;

namespace NodeEditor.Plugins.LLMTornado.Contexts;

public sealed class SimpleChatContext : INodeContext
{
    [Node("Simple Chat",
        category: "LLM/Chat",
        description: "Send a prompt to an LLM and get a text response",
        isCallable: true)]
    public void SimpleChat(
        string Prompt,
        string? SystemMessage,
        string? Model,
        out string Response,
        out int TokensUsed,
        out string Error,
        out ExecutionPath Next)
    {
        Next = new ExecutionPath();
        Response = string.Empty;
        TokensUsed = 0;
        Error = string.Empty;

        try
        {
            var config = LLMTornadoConfiguration.FromEnvironment();
            var api = config.CreateApi();

            var messages = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(SystemMessage))
            {
                messages.Add(new ChatMessage(ChatMessageRoles.System, SystemMessage));
            }
            messages.Add(new ChatMessage(ChatMessageRoles.User, Prompt ?? string.Empty));

            var request = new ChatRequest
            {
                Messages = messages
            };

            if (!string.IsNullOrWhiteSpace(Model))
            {
                request.Model = (ChatModel)Model;
            }

            var result = api.Chat.CreateChatCompletion(request).GetAwaiter().GetResult();

            if (result?.Choices is { Count: > 0 })
            {
                Response = result.Choices[0].Message?.Content ?? string.Empty;
            }

            if (result?.Usage is not null)
            {
                TokensUsed = result.Usage.PromptTokens + result.Usage.CompletionTokens;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Error = ex.InnerException?.Message ?? ex.Message;
        }

        Next.Signal();
    }

    [Node("Chat Completion",
        category: "LLM/Chat",
        description: "Send structured messages and receive full response metadata",
        isCallable: true)]
    public void ChatCompletion(
        ChatMessage[] Messages,
        string? Model,
        out string ResponseText,
        out int PromptTokens,
        out int CompletionTokens,
        out string FinishReason,
        out string Error,
        out ExecutionPath Next)
    {
        Next = new ExecutionPath();
        ResponseText = string.Empty;
        PromptTokens = 0;
        CompletionTokens = 0;
        FinishReason = string.Empty;
        Error = string.Empty;

        try
        {
            var config = LLMTornadoConfiguration.FromEnvironment();
            var api = config.CreateApi();

            var request = new ChatRequest
            {
                Messages = Messages?.ToList() ?? new List<ChatMessage>()
            };

            if (!string.IsNullOrWhiteSpace(Model))
            {
                request.Model = (ChatModel)Model;
            }

            var result = api.Chat.CreateChatCompletion(request).GetAwaiter().GetResult();

            if (result?.Choices is { Count: > 0 })
            {
                ResponseText = result.Choices[0].Message?.Content ?? string.Empty;
                FinishReason = result.Choices[0].FinishReason?.ToString() ?? string.Empty;
            }

            if (result?.Usage is not null)
            {
                PromptTokens = result.Usage.PromptTokens;
                CompletionTokens = result.Usage.CompletionTokens;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Error = ex.InnerException?.Message ?? ex.Message;
        }

        Next.Signal();
    }

    [Node("Vision Chat",
        category: "LLM/Chat",
        description: "Send a prompt with an image URL to a vision model",
        isCallable: true)]
    public void VisionChat(
        string Prompt,
        string ImageUrl,
        string? SystemMessage,
        string? Model,
        out string Response,
        out int TokensUsed,
        out string Error,
        out ExecutionPath Next)
    {
        Next = new ExecutionPath();
        Response = string.Empty;
        TokensUsed = 0;
        Error = string.Empty;

        try
        {
            var config = LLMTornadoConfiguration.FromEnvironment();
            var api = config.CreateApi();

            var messages = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(SystemMessage))
            {
                messages.Add(new ChatMessage(ChatMessageRoles.System, SystemMessage));
            }

            var parts = new List<ChatMessagePart>
            {
                new(Prompt ?? string.Empty),
                new(new ChatImage(ImageUrl ?? string.Empty))
            };
            messages.Add(new ChatMessage(ChatMessageRoles.User, parts));

            var request = new ChatRequest
            {
                Messages = messages
            };

            if (!string.IsNullOrWhiteSpace(Model))
            {
                request.Model = (ChatModel)Model;
            }

            var result = api.Chat.CreateChatCompletion(request).GetAwaiter().GetResult();

            if (result?.Choices is { Count: > 0 })
            {
                Response = result.Choices[0].Message?.Content ?? string.Empty;
            }

            if (result?.Usage is not null)
            {
                TokensUsed = result.Usage.PromptTokens + result.Usage.CompletionTokens;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Error = ex.InnerException?.Message ?? ex.Message;
        }

        Next.Signal();
    }
}
