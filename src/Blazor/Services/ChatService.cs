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

        // 1. Skapa engelska sökord om frågan är på svenska
        var keywordPrompt = $"""
             Analysera användarens fråga eller uttryck och identifiera det underliggande personal-, arbetsmiljö- eller HR-relaterade ämnet (även om uttrycket är kort, vardagligt eller implicit).
            Skapa 2-4 relevanta engelska sökord som bäst matchar hur detta ämne beskrivs i en professionell personalhandbok.
            Svara ENDAST med de engelska sökorden separerade med mellanslag, absolut ingenting annat.

            Fråga: {question}
            Sökord:
            """;

        var searchKeywords = await _openAiService.SendMessageAsync(keywordPrompt, ct: ct);
        var searchQuery = string.IsNullOrWhiteSpace(searchKeywords) ? question : searchKeywords.Trim();

        // 2. Sök relevanta textbitar i indexet med sökorden
        var chunks = await _searchService.SearchAsync(searchQuery, top: 3, ct: ct);

        // Inget hittades i sökindexet -> IsRefused = true
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

        // 4. Bygg prompten 
        var prompt = $"""
            Du är en hjälpsam personalassistent för Kalle Anka AB.
            Ditt uppdrag är att vägleda medarbetare i frågor om personal, anställningsvillkor, förmåner, arbetsmiljö och säkerhet.
            Svara alltid på samma språk som medarbetaren ställer frågan på (svenska om frågan är på svenska).
            Förståelse och bemötande:
            - Tolka användarens avsikt välvilligt: även korta, vardagliga eller implicita uttryck (t.ex. uttryck för smärta, oro, hälsa eller missnöje) ska kopplas till relevanta rutiner och riktlinjer i handboken.
            - Ge ett empatiskt, tydligt och praktiskt råd utifrån handbokens rutiner om informationen finns i utdragen.
            Viktiga regler som du MÅSTE följa:
            1. Basera ditt svar ENBART på informationen i de bifogade utdragen nedan.
            2. Hitta INTE på information som inte finns i utdragen. Om utdragen INTE innehåller relevant information för att hjälpa medarbetaren, inled svaret med ordet "AVBÖJER:" följt av en vänlig förklaring att information saknas i personalhandboken.
            3. Besvara ENDAST personal- och arbetsrelaterade ärenden. Om frågan helt saknar koppling till arbetsplatsen eller personalfrågor (t.ex. allmänbildning, väder, matlagning eller sport), inled svaret med ordet "AVBÖJER:" följt av en vänlig förklaring att du enbart hanterar personalfrågor.
            4. Skriv svaret i ren, oformaterad text (plain text). Använd ALDRIG Markdown-formatering: inga asterisker för fetstil (**ord** eller *ord*), inga taggar (#) och inga kodblock. För punktlistor, använd vanliga bindestreck (-).

            Bifogade utdrag ur personalhandboken:
            {contextBuilder}

            Fråga från medarbetare:
            {question}

            Svar:
            """;

        // 5. Ställ frågan till OpenAI
        var answer = await _openAiService.SendMessageAsync(prompt, ct: ct);

        // Om modellen avböjde (ej personalfråga eller saknas i handboken) -> IsRefused = true
        if (answer.Contains("bara besvara frågor", StringComparison.OrdinalIgnoreCase) ||
            answer.Contains("saknas i personalhandboken", StringComparison.OrdinalIgnoreCase) ||
            answer.Contains("saknas information", StringComparison.OrdinalIgnoreCase))
        {
            return ChatResponse.Refused(answer);
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