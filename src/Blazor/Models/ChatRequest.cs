namespace Shared.Models;

/// <summary>Ett meddelande i en konversation. Reserverat för följdfrågor (används ej i MVP).</summary>
public sealed record ChatMessage(string Role, string Content)
{
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";
}

/// <summary>Fråga från användaren. ConversationId och History är förberedda för senare.</summary>
public sealed record ChatRequest(
    string Question,
    string? ConversationId = null,
    List<ChatMessage>? History = null);
