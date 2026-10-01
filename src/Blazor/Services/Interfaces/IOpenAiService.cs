namespace Blazor.Services.Interfaces;

/// <summary>Kommunicerar med Azure OpenAI.</summary>
public interface IOpenAiService
{
    /// <summary>Skickar en prompt till modellen och returnerar svaret.</summary>
    Task<string> SendMessageAsync(string prompt, CancellationToken ct = default);

    /// <summary>Strömmar modellens svar som textbitar (tokens) allteftersom de genereras.</summary>
    IAsyncEnumerable<string> SendMessageStreamingAsync(string prompt, CancellationToken ct = default);
}