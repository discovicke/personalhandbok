using System.ClientModel;
using System.Runtime.CompilerServices;
using OpenAI;
using OpenAI.Chat;
using Blazor.Config;
using Blazor.Services.Interfaces;

namespace Blazor.Services;

/// <summary>Pratar med Azure OpenAI / AI Foundry med inställningar från EnvLoader.</summary>
public sealed class AzureOpenAiService : IOpenAiService
{
    private readonly ChatClient _chatClient;

    public AzureOpenAiService()
    {
        var endpoint = EnvLoader.GetRequired("OPENAI_ENDPOINT");
        var key = EnvLoader.GetRequired("OPENAI_API_KEY");
        var deploymentName = EnvLoader.GetRequired("OPENAI_DEPLOYMENT_NAME");

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(endpoint)
        };

        var client = new OpenAIClient(new ApiKeyCredential(key), clientOptions);
        _chatClient = client.GetChatClient(deploymentName);
    }

    /// <inheritdoc/>
    public async Task<string> SendMessageAsync(string prompt, CancellationToken ct = default)
    {
        ClientResult<ChatCompletion> result = await _chatClient.CompleteChatAsync(
            [new UserChatMessage(prompt)], 
            cancellationToken: ct);

        return result.Value.Content.Count > 0 
            ? result.Value.Content[0].Text 
            : string.Empty;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> SendMessageStreamingAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var updates = _chatClient.CompleteChatStreamingAsync(
            [new UserChatMessage(prompt)],
            cancellationToken: ct);

        await foreach (var update in updates)
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                    yield return part.Text;
            }
        }
    }
}