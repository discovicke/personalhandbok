namespace Shared.Models;

public sealed record ChatMessage(string Role, string Content)
{
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";
}

public sealed record ChatRequest(
    string Question,
    string? ConversationId = null,
    List<ChatMessage>? History = null);
