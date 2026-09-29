using System.ClientModel;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using Shared.Config;

namespace Blazor.Services;

/// <summary>Pratar med Azure OpenAI med inställningar från EnvLoader.</summary>
public sealed class AzureOpenAiService : IOpenAiService
{
    private readonly ChatClient _chatClient;

    public AzureOpenAiService()
    {
        var endpoint = EnvLoader.GetRequired("OPENAI_ENDPOINT");
        var key = EnvLoader.GetRequired("OPENAI_API_KEY");
        var deploymentName = EnvLoader.GetRequired("OPENAI_DEPLOYMENT_NAME");

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(key));
        _chatClient = azureClient.GetChatClient(deploymentName);
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