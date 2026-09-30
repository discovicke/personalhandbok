using System.Text;
using Shared.Models;

namespace Blazor.Services;

/// <summary>RAG-chattjänst som kombinerar Azure AI Search med Azure OpenAI.</summary>
public sealed class ChatService : IChatService
{
    private readonly ISearchService _searchService;
    private readonly IOpenAiService _openAiService;

    public ChatService(ISearchService searchService, IOpenAiService openAiService)
    {
        _searchService = searchService;
        _openAiService = openAiService;
    }

    /// <inheritdoc/>
    public async Task<ChatResponse> AskAsync(string question, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return new ChatResponse(
                Answer: "Ställ en fråga för att få ett svar.",
                IsRefused: false,
                RefusalReason: null,
                Citations: []);
        }

        // 1. Skapa engelska sökord om frågan är på svenska 
        var keywordPrompt = $"""
            Skapa 2-4 engelska sökord (keywords) för att söka i en engelsk personalhandbok baserat på användarens fråga.
            Svara ENDAST med de engelska sökorden separerade med mellanslag, absolut ingenting annat.

            Fråga: {question}
            Sökord:
            """;

        var searchKeywords = await _openAiService.SendMessageAsync(keywordPrompt, ct: ct);
        var searchQuery = string.IsNullOrWhiteSpace(searchKeywords) ? question : searchKeywords.Trim();

        // 2. Sök relevanta textbitar i indexet med sökorden
        var chunks = await _searchService.SearchAsync(searchQuery, top: 3, ct: ct);

        if (chunks.Count == 0)
        {
            return new ChatResponse(
                Answer: "Jag hittade tyvärr ingen information om detta i personalhandboken.",
                IsRefused: false,
                RefusalReason: null,
                Citations: []);
        }

        // 3. Sammanställ kontext från träffarna
        var contextBuilder = new StringBuilder();
        foreach (var chunk in chunks)
        {
            contextBuilder.AppendLine($"[Dokument: {chunk.FileName}, Avsnitt/Sida: {chunk.ChunkIndex}]");
            contextBuilder.AppendLine(chunk.Content);
            contextBuilder.AppendLine();
        }

        // 4. Bygg prompt med strikta regler
        var prompt = $"""
            Du är en hjälpsam personalassistent för Kalle Anka AB.
            Ditt uppdrag är att besvara medarbetares frågor om personalfrågor, förmåner och regler.
            Svara alltid på samma språk som medarbetaren ställer frågan på (svenska om frågan är på svenska).

            Viktiga regler som du MÅSTE följa:
            1. Basera ditt svar ENBART på informationen i de bifogade utdragen nedan.
            2. Hitta INTE på information som inte finns i utdragen. Om informationen inte räcker, svara att det saknas i personalhandboken.
            3. Besvara ENDAST personal- och arbetsrelaterade frågor. Om användaren frågar om något helt annat (t.ex. programmering, allmänbildning eller väder), avböj vänligt och förklara att du bara besvarar frågor gällande personalhandboken.

            Bifogade utdrag ur personalhandboken:
            {contextBuilder}

            Fråga från medarbetare:
            {question}

            Svar:
            """;

        // 5. Ställ frågan till OpenAI
        var answer = await _openAiService.SendMessageAsync(prompt, ct: ct);

        // 6. Skapa källhänvisningar (Citations)
        var citations = chunks.Select(c => new Citation(
            DocumentId: c.DocumentId,
            FileName: c.FileName,
            Quote: c.Content.Length > 200 ? c.Content[..200] + "..." : c.Content,
            ChunkId: c.ChunkId
        )).ToList();

        return new ChatResponse(
            Answer: answer,
            IsRefused: false,
            RefusalReason: null,
            Citations: citations);
    }
}