using System.Runtime.CompilerServices;
using System.Text;
using Blazor.Models;
using Blazor.Services.Interfaces;

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
    public async Task<ChatResponse> AskAsync(
        string question, 
        IReadOnlyList<ChatMessage>? history = null, 
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return ChatResponse.Refused("Ställ en fråga för att få ett svar.");
        }

        // 1. Sök relevanta textbitar i indexet med hybridsökning
        var chunks = await _searchService.SearchAsync(question, top: 5, ct: ct);

        // Inget hittades i sökindexet -> IsRefused = true
        if (chunks.Count == 0)
        {
            return ChatResponse.Refused("Jag hittade tyvärr ingen information om detta i personalhandboken.");
        }

        // 2. Sammanställ kontext från träffarna
        var context = BuildContext(chunks);

        // 3. Bygg prompten med historik
        var prompt = BuildPrompt(question, context, history);

        // 4. Ställ frågan till OpenAI
        var answer = await _openAiService.SendMessageAsync(prompt, ct: ct);

        // 5. Om modellen avböjde -> IsRefused = true med rensad text
        if (IsRefusalAnswer(answer))
        {
            return ChatResponse.Refused(ChatStreamHelper.TrimRefusalPrefix(answer));
        }

        // 6. Skapa källhänvisningar (Citations)
        return new ChatResponse(
            Answer: answer,
            IsRefused: false,
            RefusalReason: null,
            Citations: BuildCitations(chunks));
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatStreamUpdate> AskStreamingAsync(
        string question,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            yield return new ChatStreamUpdate(
                ChatStreamStatus.Done, null,
                ChatResponse.Refused("Ställ en fråga för att få ett svar."));
            yield break;
        }

        yield return new ChatStreamUpdate(ChatStreamStatus.Searching, null, null);

        var chunks = await _searchService.SearchAsync(question, top: 5, ct: ct);

        if (chunks.Count == 0)
        {
            yield return new ChatStreamUpdate(
                ChatStreamStatus.Done, null,
                ChatResponse.Refused("Jag hittade tyvärr ingen information om detta i personalhandboken."));
            yield break;
        }

        var prompt = BuildPrompt(question, BuildContext(chunks));

        yield return new ChatStreamUpdate(ChatStreamStatus.Thinking, null, null);

        // Strömma tokens, buffra till hela ord och håll tillbaka AVBÖJER-markören
        // så den aldrig skickas ut i deltan.
        var fullAnswer = new StringBuilder();
        var pending = new StringBuilder();
        var prefixDecisionMade = false;

        await foreach (var token in _openAiService.SendMessageStreamingAsync(prompt, ct))
        {
            fullAnswer.Append(token);
            pending.Append(token);

            if (!prefixDecisionMade)
            {
                if (ChatStreamHelper.CouldBeRefusalPrefix(fullAnswer.ToString()))
                    continue; // Kan vara början på AVBÖJER: — vänta på fler tecken.

                prefixDecisionMade = true;
                if (ChatStreamHelper.StartsWithRefusalPrefix(fullAnswer.ToString()))
                {
                    // Ta bort markören från det som ska skickas ut.
                    pending.Clear();
                    pending.Append(ChatStreamHelper.TrimRefusalPrefix(fullAnswer.ToString()));
                }
            }

            var words = ChatStreamHelper.ExtractCompleteWords(pending);
            if (words.Length > 0)
                yield return new ChatStreamUpdate(ChatStreamStatus.Token, words, null);
        }

        var tail = pending.ToString();
        if (tail.Length > 0)
            yield return new ChatStreamUpdate(ChatStreamStatus.Token, tail, null);

        var answer = fullAnswer.ToString();
        if (IsRefusalAnswer(answer))
        {
            // Spara trimmad text så AVBÖJER aldrig når frontend.
            yield return new ChatStreamUpdate(
                ChatStreamStatus.Done, null,
                ChatResponse.Refused(ChatStreamHelper.TrimRefusalPrefix(answer)));
        }
        else
        {
            yield return new ChatStreamUpdate(
                ChatStreamStatus.Done, null,
                new ChatResponse(answer, false, null, BuildCitations(chunks)));
        }
    }

    private static string BuildContext(IReadOnlyList<SearchChunk> chunks)
    {
        var contextBuilder = new StringBuilder();
        foreach (var chunk in chunks)
        {
            contextBuilder.AppendLine($"[Dokument: {chunk.FileName}, Avsnitt/Sida: {chunk.ChunkIndex}]");
            contextBuilder.AppendLine(chunk.Content);
            contextBuilder.AppendLine();
        }
        return contextBuilder.ToString();
    }

    private static string BuildPrompt(string question, string context, IReadOnlyList<ChatMessage>? history = null)
    {
        var historyBuilder = new StringBuilder();
        if (history is not null && history.Count > 0)
        {
            historyBuilder.AppendLine("Tidigare meddelanden i samtalet (använd som sammanhang vid följdfrågor):");
            var recentHistory = history.TakeLast(6);
            foreach (var msg in recentHistory)
            {
                var roleLabel = msg.Role == ChatMessage.UserRole ? "Medarbetare" : "Assistent";
                historyBuilder.AppendLine($"{roleLabel}: {msg.Content}");
            }
            historyBuilder.AppendLine();
        }

        return $"""
            Du är en hjälpsam personalassistent för Kalle Anka AB.
            Ditt uppdrag är att vägleda medarbetare i frågor om personal, anställningsvillkor, förmåner, arbetsmiljö och säkerhet.
            Svara alltid på naturlig, professionell och korrekt svenska.

            Språk och översättning:
            - Utdragen ur personalhandboken är ofta skrivna på engelska. Du MÅSTE översätta ALLA engelska begrepp, förmåner, titlar och beskrivningar till naturlig svenska. Inga engelska fraser eller uttryck får lämnas oöversatta i svaret.
            - Etablerade program- och produktnamn (som PerksPlus eller Northwind) kan nämnas vid namn, men all förklarande text och alla förmåner ska vara på ren svenska.
            - Koppla medarbetarens svenska frågor till motsvarande engelska begrepp i utdragen.

            Bemötande och eskalering:
            - Följdfrågor och kontext: Använd tidigare meddelanden i samtalet för att förstå följdfrågor (t.ex. "vad menade du med det?" eller "hur många veckor var den högsta nivån?").
            - Frågor eller konflikter som rör närmaste chef: Om medarbetarens ärende, konflikt eller missnöje berör den egna chefen, ska du ALDRIG hänvisa till chefen själv. Hänvisa istället uteslutande till alternativa vägar: HR/personalavdelningen, överordnad chef, skyddsombud eller företagets compliance/visselblåsarfunktion.
            - Våld, hot eller olagligheter: Du får aldrig hjälpa till med våld, att skada någon eller begå brott. Vid hot, aggressioner eller våld på arbetsplatsen ska du hänvisa till företagets nolltolerans mot arbetsplatsvåld, HR, skyddsombud och vid akut fara larmnumret 112.
            - Tolka användarens avsikt välvilligt: även korta eller vardagliga frågor ska besvaras med relevanta rutiner och riktlinjer om de finns i texten.

            Regler för svar och avböjning:
            1. Basera ditt svar ENBART på informationen i de bifogade utdragen nedan samt tidigare meddelanden i samtalet. Hitta INTE på fakta som inte stöds av texten.
            2. Om utdragen innehåller information som berör frågan (även om det bara är delar eller en översikt), ska du BESVARA frågan med den fakta som finns (översatt till svenska). Om specifika detaljer saknas, nämn i slutet vad som inte framgår i handboken. Använd INTE ordet "AVBÖJER" i dessa fall.
            3. Inled svaret med ordet "AVBÖJER:" ENBART om:
               - Utdragen HELT saknar relevant information om det efterfrågade ämnet, ELLER
               - Frågan saknar koppling till personalfrågor eller arbetsplatsen (t.ex. väder, allmänbildning, recept).
            4. Skriv svaret i ren text (plain text) utan Markdown: inga asterisker (**fetstil**), inga taggar (#) och inga kodblock. För punktlistor, använd vanliga bindestreck (-).
            5. Använd uteslutande det latinska alfabetet (med å, ä, ö), siffror och vanliga skiljetecken. Använd aldrig tecken från andra skriftsystem.

            {historyBuilder}Bifogade utdrag ur personalhandboken:
            {context}

            Aktuell fråga från medarbetare:
            {question}

            Svar:
            """;
    }

    /// <summary>True om modellen avböjde (ej personalfråga eller saknas i handboken).</summary>
    private static bool IsRefusalAnswer(string answer) =>
        ChatStreamHelper.StartsWithRefusalPrefix(answer) ||
        answer.Contains("bara besvara frågor", StringComparison.OrdinalIgnoreCase) ||
        answer.Contains("saknas i personalhandboken", StringComparison.OrdinalIgnoreCase) ||
        answer.Contains("saknas information", StringComparison.OrdinalIgnoreCase);

    private static List<Citation> BuildCitations(IReadOnlyList<SearchChunk> chunks) =>
        chunks.Select(c => new Citation(
            DocumentId: c.DocumentId,
            FileName: c.FileName,
            Quote: c.Content.Length > 200
                ? c.Content[..200] + "..."
                : c.Content,
            ChunkId: c.ChunkIndex > 0
                ? $"Sida {c.ChunkIndex}"
                : null
        )).ToList();
}