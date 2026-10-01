using Blazor.Models;

namespace Blazor.Services.Interfaces;

/// <summary>Tjänst för att besvara personalfrågor via RAG (Search + OpenAI).</summary>
public interface IChatService
{
    /// <summary>Söker relevanta textbitar och ställer frågan till modellen med kontext och samtalshistorik.</summary>
    Task<ChatResponse> AskAsync(
        string question, 
        IReadOnlyList<ChatMessage>? history = null, 
        CancellationToken ct = default);

    /// <summary>Strömmar svaret: Searching (söker i dokument), Thinking (tänker), Token (ord) och Done (slutgiltigt svar).</summary>
    IAsyncEnumerable<ChatStreamUpdate> AskStreamingAsync(string question, CancellationToken ct = default);
}