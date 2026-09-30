using Shared.Models;

namespace Blazor.Services;

/// <summary>Tjänst för att besvara personalfrågor via RAG (Search + OpenAI).</summary>
public interface IChatService
{
    /// <summary>Söker relevanta textbitar och ställer frågan till modellen med kontext.</summary>
    Task<ChatResponse> AskAsync(string question, CancellationToken ct = default);
}