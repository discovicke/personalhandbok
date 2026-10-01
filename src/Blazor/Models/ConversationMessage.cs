namespace Blazor.Models;

/// <summary>Ett meddelande i chattvyn som sparas i webbläsarens localStorage.</summary>
public sealed record ConversationMessage(
    string Role,
    string Content,
    bool IsRefused = false,
    string? RefusalReason = null,
    List<Citation>? Citations = null,
    DateTime? Timestamp = null)
{
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";

    /// <summary>Skapar ett användarmeddelande med aktuell tidstämpel.</summary>
    public static ConversationMessage CreateUser(string question) =>
        new(UserRole, question, Timestamp: DateTime.Now);

    /// <summary>Skapar ett assistentmeddelande baserat på svaret från ChatService.</summary>
    public static ConversationMessage CreateAssistant(ChatResponse response) =>
        new(
            Role: AssistantRole,
            Content: response.Answer,
            IsRefused: response.IsRefused,
            RefusalReason: response.RefusalReason,
            Citations: response.Citations,
            Timestamp: DateTime.Now);

    /// <summary>Konverterar till den enkla ChatMessage-modellen som skickas till AI:n som historik.</summary>
    public ChatMessage ToChatMessage() =>
        new(Role, IsRefused ? (RefusalReason ?? string.Empty) : Content);
}