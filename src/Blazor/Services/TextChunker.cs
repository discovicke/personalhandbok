using Shared.Models;

namespace Blazor.Services;

/// <summary>Delar lång text i sökbara bitar (<see cref="SearchChunk"/>).</summary>
public static class TextChunker
{
    /// <summary>
    /// Delar text i bitar om max <paramref name="maxChars"/> tecken med <paramref name="overlap"/> tecken överlapp.
    /// Snittar vid meningsgräns, aldrig mitt i ett ord. Deterministisk: samma indata ger alltid samma bitar.
    /// </summary>
    /// <param name="text">Redan utläst klartext (PDF/Word-tolkning sker i upload-steget, inte här).</param>
    /// <param name="documentId">Dokumentets id. Bakas in i varje ChunkId så omindexering skriver över.</param>
    /// <param name="fileName">Filnamn som följer med varje bit för visning och sökning.</param>
    /// <param name="maxChars">Max tecken per bit. Standard 1000.</param>
    /// <param name="overlap">Tecken som återanvänds från förra biten så meningar vid skarven inte kapas. Standard 150.</param>
    /// <returns>En eller flera bitar med löpande ChunkIndex från 0.</returns>
    public static IReadOnlyList<SearchChunk> Chunk(
        string text,
        string documentId,
        string fileName,
        int maxChars = 1000,
        int overlap = 150)
    {
        // Ogiltiga storlekar stoppas direkt — annars kan loopen nedan aldrig avslutas.
        if (maxChars <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxChars), "Måste vara större än 0.");
        if (overlap < 0 || overlap >= maxChars)
            throw new ArgumentOutOfRangeException(nameof(overlap), "Måste vara minst 0 och mindre än maxChars.");
        if (string.IsNullOrWhiteSpace(documentId))
            throw new ArgumentException("Krävs eftersom det bakas in i varje ChunkId.", nameof(documentId));

        // Tom text ger noll bitar — inget att indexera.
        if (string.IsNullOrWhiteSpace(text))
            return [];

        // Kort text behöver ingen delning: exakt en bit med index 0.
        if (Measure(text) <= maxChars)
            return [new SearchChunk($"{documentId}-0", documentId, fileName, text, 0)];

        // Huvudloop: varje varv tar en bit, sedan flyttas start framåt med
        // (maxChars - overlap) så nästa bit återanvänder 150 tecken från förra biten.
        var chunks = new List<SearchChunk>();
        var start = 0;
        while (start < text.Length)
        {
            // Råslut 1000 tecken framåt, eller textens slut om det är sista biten.
            var end = Math.Min(start + maxChars, text.Length);

            // Backa till meningsgräns så svaret aldrig börjar mitt i en mening.
            if (end < text.Length)
                end = FindBoundary(text, start, end);

            // Klipp ut biten. Trim tar bort hängande radbrytningar vid skarven.
            var content = text[start..end].Trim();
            if (Measure(content) > 0)
            {
                // Stabilt id: samma fil + samma löpnummer ger samma ChunkId,
                // så ersättning av filen skriver över istället för att duplicera.
                var index = chunks.Count;
                chunks.Add(new SearchChunk($"{documentId}-{index}", documentId, fileName, content, index));
            }

            // Slutet nått — klart. Annars kliv framåt från det faktiska snittet
            // så överlappet räknas mot verklig skarv, inte råpositionen.
            if (end >= text.Length)
                break;
            var next = end - overlap;
            // Skydd mot stillastående: om snittjusteringen ätit upp steget, tvinga minsta kliv.
            start = next > start ? next : start + (maxChars - overlap);
        }

        return chunks;
    }

    /// <summary>
    /// Hittar bästa snittpositionen genom att söka bakåt från <paramref name="end"/>:
    /// meningsgräns först (. ! ? eller dubbelradbrytning), annars mellanslag. Aldrig mitt i ett ord.
    /// </summary>
    /// <returns>Justerad slutposition, alltid större än <paramref name="start"/>.</returns>
    private static int FindBoundary(string text, int start, int end)
    {
        // Sökfönster bakåt: max 200 tecken så biten inte krymper för mycket.
        var floor = Math.Max(start + 1, end - 200);

        //  leta efter meningsslut följt av blanksteg/radbrytning.
        for (var i = end - 1; i >= floor; i--)
        {
            if (text[i] is '.' or '!' or '?' && (i + 1 >= end || char.IsWhiteSpace(text[i + 1])))
                return i + 1;
            // Dubbelradbrytning = styckesgräns, ännu bättre snitt.
            if (text[i] == '\n' && i > floor && text[i - 1] == '\n')
                return i + 1;
        }

        //  inget meningsslut hittat — fall tillbaka på närmaste mellanslag.
        for (var i = end - 1; i >= floor; i--)
        {
            if (char.IsWhiteSpace(text[i]))
                return i + 1;
        }

        //  ett enda långt ord utan blanksteg — hårt snitt är enda alternativet.
        return end;
    }

    /// <summary>
    /// Mäter bitstorlek. Idag tecken — byt kropp mot tokenizer (t.ex. Tiktoken) om
    /// vi går över till vektor/embeddings där exakta tokenantal spelar roll.
    /// </summary>
    private static int Measure(string s) => s.Length;
}
