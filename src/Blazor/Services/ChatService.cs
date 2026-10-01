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
    public async Task<ChatResponse> AskAsync(string question, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return ChatResponse.Refused("Ställ en fråga för att få ett svar.");
        }

        // 1. Sök direkt på frågan. Hybridsökningen (text + vektor) förstår svenska frågor
        // mot engelskt innehåll utan översättning. LLM behövs inte före sökningen.
        var chunks = await _searchService.SearchAsync(question, top: 5, ct: ct);

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
            Svara alltid på naturlig, professionell och korrekt svenska.

            Språk och översättning:
            - Utdragen ur personalhandboken är ofta skrivna på engelska. Du MÅSTE översätta ALLA engelska begrepp, förmåner, titlar och beskrivningar till naturlig svenska. Inga engelska fraser eller uttryck får lämnas oöversatta i svaret.
            - Etablerade program- och produktnamn (som PerksPlus eller Northwind) kan nämnas vid namn, men all förklarande text och alla förmåner ska vara på ren svenska.
            - Koppla medarbetarens svenska frågor till motsvarande engelska begrepp i utdragen.
            
            Bemötande och eskalering:
            - Frågor eller konflikter som rör närmaste chef: Om medarbetarens ärende, konflikt eller missnöje berör den egna chefen, ska du ALDRIG hänvisa till chefen själv. Hänvisa istället uteslutande till alternativa vägar: HR/personalavdelningen, överordnad chef, skyddsombud eller företagets compliance/visselblåsarfunktion.
            - Våld, hot eller olagligheter: Du får aldrig hjälpa till med våld, att skada någon eller begå brott. Vid hot, aggressioner eller våld på arbetsplatsen ska du hänvisa till företagets nolltolerans mot arbetsplatsvåld, HR, skyddsombud och vid akut fara larmnumret 112.
            - Tolka användarens avsikt välvilligt: även korta eller vardagliga frågor ska besvaras med relevanta rutiner och riktlinjer om de finns i texten.
            
            Regler för svar och avböjning:
            1. Basera ditt svar ENBART på informationen i de bifogade utdragen nedan. Hitta INTE på fakta som inte stöds av texten.
            2. Om utdragen innehåller information som berör frågan (även om det bara är delar eller en översikt), ska du BESVARA frågan med den fakta som finns (översatt till svenska). Om specifika detaljer saknas, nämn i slutet vad som inte framgår i handboken. Använd INTE ordet "AVBÖJER" i dessa fall.
            3. Inled svaret med ordet "AVBÖJER:" ENBART om:
               - Utdragen HELT saknar relevant information om det efterfrågade ämnet, ELLER
               - Frågan saknar koppling till personalfrågor eller arbetsplatsen (t.ex. väder, allmänbildning, recept).
            4. Skriv svaret i ren text (plain text) utan Markdown: inga asterisker (**fetstil**), inga taggar (#) och inga kodblock. För punktlistor, använd vanliga bindestreck (-).
            5. Använd uteslutande det latinska alfabetet (med å, ä, ö), siffror och vanliga skiljetecken. Använd aldrig tecken från andra skriftsystem.

            
            Bifogade utdrag ur personalhandboken:
            {contextBuilder}

            Fråga från medarbetare:
            {question}

            Svar:
            """;

        // 5. Ställ frågan till OpenAI
        var answer = await _openAiService.SendMessageAsync(prompt, ct: ct);

        // Om modellen avböjde (ej personalfråga eller saknas i handboken) -> IsRefused = true
        // Prompten ber modellen inleda avböjda svar med "AVBÖJER:", så det är det vi måste detektera.
        var trimmedAnswer = answer.TrimStart();
        if (trimmedAnswer.StartsWith("AVBÖJER:", StringComparison.OrdinalIgnoreCase) ||
            trimmedAnswer.StartsWith("AVBOJER:", StringComparison.OrdinalIgnoreCase) ||
            answer.Contains("bara besvara frågor", StringComparison.OrdinalIgnoreCase) ||
            answer.Contains("saknas i personalhandboken", StringComparison.OrdinalIgnoreCase) ||
            answer.Contains("saknas information", StringComparison.OrdinalIgnoreCase))
        {
            return ChatResponse.Refused(answer);
        }

        // 6. Skapa källhänvisningar (Citations)
        var citations = chunks.Select(c => new Citation(
            DocumentId: c.DocumentId,
            FileName: c.FileName,
            Quote: c.Content.Length > 200 
                ? c.Content[..200] + "..." 
                : c.Content,
            ChunkId: c.ChunkIndex > 0 
                ? $"Sida {c.ChunkIndex}" 
                : null
        )).ToList();

        return new ChatResponse(
            Answer: answer,
            IsRefused: false,
            RefusalReason: null,
            Citations: citations);
    }
}