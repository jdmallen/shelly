using System.ClientModel;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace JDMallen.Shelly;

public sealed class AzureChatProvider : IChatProvider
{
	internal const string ApiKeyEnvVar = "AZURE_OPENAI_API_KEY";
	internal const string EndpointEnvVar = "AZURE_OPENAI_ENDPOINT_HOST";
	internal const string DeploymentEnvVar = "AZURE_OPENAI_DEPLOYMENT";

	private readonly ChatClient _chatClient;

	public AzureChatProvider(string apiKey, string endpoint, string deployment)
	{
		var azureClient = new AzureOpenAIClient(
			new Uri(endpoint),
			new ApiKeyCredential(apiKey));
		_chatClient = azureClient.GetChatClient(deployment);
	}

	public Task<string> GetSuggestionAsync(
		string prompt,
		string context,
		CancellationToken cancellationToken = default)
		=> ChatAsync(Prompts.Suggestion(context), prompt, sanitize: true, cancellationToken);

	public Task<string> ExplainCommandAsync(
		string command,
		string context,
		CancellationToken cancellationToken = default)
		=> ChatAsync(Prompts.Explain(context), command, sanitize: false, cancellationToken);

	private async Task<string> ChatAsync(
		string system,
		string userMessage,
		bool sanitize,
		CancellationToken cancellationToken)
	{
		var options = new ChatCompletionOptions
		{
			MaxOutputTokenCount = 500,
			Temperature = 0.1f,
		};

		ChatMessage[] messages =
		[
			new SystemChatMessage(system),
			new UserChatMessage(userMessage),
		];

		ClientResult<ChatCompletion> result = await _chatClient.CompleteChatAsync(
			messages,
			options,
			cancellationToken);

		string text = string.Concat(result.Value.Content.Select(p => p.Text)).Trim();

		return sanitize ? CommandSanitizer.Strip(text) : text;
	}
}
