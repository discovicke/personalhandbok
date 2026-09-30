using System.Text;
using Shared.Models;

namespace Blazor.Services;

/// <summary>RAG-chattjänst som kombinerar Azure AI Search med Azure OpenAI och hanterar avböjande svar.</summary>
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
            return ChatResponse.Refused("Ställ en fråga för att få ett svar.");
        }

        // 1. Kontrollera om det är en personalfråga samt generera sökord
        var checkPrompt = $"""
            Du ska avgöra om följande fråga är relevant för en personalhandbok (t.ex. anställningsvillkor, förmåner, semester, lön, regler, roller, hälsa, arbetsmiljö eller företaget).
            Om frågan INTE rör personal eller arbetsplatsen (t.ex. allmänbildning, väder, programmering, sport eller recept), svara med exakt "EJ_PERSONAL".
            Om frågan ÄR relevant, svara med 2-4 engelska sökord (keywords) för att söka i handboken separerade med mellanslag.
            Svara ENDAST med "EJ_PERSONAL" eller sökorden, absolut ingenting annat.

            Fråga: {question}
            Svar:
            """;

        var checkResult = await _openAiService.SendMessageAsync(checkPrompt, ct: ct);
        var cleanResult = checkResult.Trim();

        // Inte en personalfråga -> Neka med snäll text
        if (cleanResult.StartsWith("EJ_PERSONAL", StringComparison.OrdinalIgnoreCase))
        {
            return ChatResponse.Refused("Jag är en personalassistent för Kalle Anka AB och kan bara svara på frågor som rör personal, anställningsvillkor och arbetsplatsen.");
        }

        // 2. Sök relevanta textbitar i indexet med de genererade sökorden
        var chunks = await _searchService.SearchAsync(cleanResult, top: 3, ct: ct);

        // Inget hittades i sökindexet -> Neka
        if (chunks.Count == 0)
        {
            return ChatResponse.Refused("Jag hittade tyvärr ingen information om detta i personalhandboken.");
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
            2. Hitta INTE på information som inte finns i utdragen. Om utdragen INTE innehåller svaret på frågan, svara med ordet "[SAKNAS]" följt av en kort och vänlig mening om att information om detta saknas i personalhandboken.
            3. Besvara ENDAST personal- och arbetsrelaterade frågor.
            4. Skriv svaret i ren, oformaterad text (plain text). Använd ALDRIG Markdown-formatering: inga asterisker för fetstil (**ord** eller *ord*), inga taggar (#) och inga kodblock. För punktlistor, använd vanliga bindestreck (-).

            Bifogade utdrag ur personalhandboken:
            {contextBuilder}

            Fråga från medarbetare:
            {question}

            Svar:
            """;

        // 5. Ställ frågan till OpenAI
        var answer = await _openAiService.SendMessageAsync(prompt, ct: ct);

        // Modellen fann inget stöd i texten -> Neka
        if (answer.StartsWith("[SAKNAS]", StringComparison.OrdinalIgnoreCase))
        {
            var refusalText = answer["[SAKNAS]".Length..].Trim();
            return ChatResponse.Refused(string.IsNullOrWhiteSpace(refusalText)
                ? "Information om detta saknas i personalhandboken."
                : refusalText);
        }

        // 6. Skapa källhänvisningar (Citations)
        var citations = chunks.Select(c => new Citation(
            DocumentId: c.DocumentId,
            FileName: c.FileName,
            Quote: c.Content.Length > 200 ? c.Content[..200] + "..." : c.Content,
            ChunkId: c.ChunkIndex > 0 ? $"Sida {c.ChunkIndex}" : null
        )).ToList();

        return new ChatResponse(
            Answer: answer,
            IsRefused: false,
            RefusalReason: null,
            Citations: citations);
    }
}