using System.ClientModel;
using OpenAI;
using OpenAI.Chat;
using Shared.Config;

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

        return result.Value.Content.Count > 0 ? result.Value.Content[0].Text : string.Empty;
    }
}