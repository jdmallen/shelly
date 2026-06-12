namespace JDMallen.Shelly;

/// <summary>
/// The single <see cref="IChatProvider" /> implementation: owns shelly's prompt
/// construction, token budget, and answer post-processing, and delegates the
/// provider-specific wire formats to a JDMallen.Toolbox.AI
/// <see cref="IChatCompletionClient" />.
/// </summary>
public sealed class ChatProvider(IChatCompletionClient client) : IChatProvider
{
	internal const string AnthropicApiKeyEnvVar = "ANTHROPIC_API_KEY_SHELLY";
	internal const string AzureApiKeyEnvVar = "AZURE_OPENAI_API_KEY";
	internal const string AzureEndpointEnvVar = "AZURE_OPENAI_ENDPOINT_HOST";
	internal const string AzureDeploymentEnvVar = "AZURE_OPENAI_DEPLOYMENT";

	private const int MAX_TOKENS = 500;

	public Task<string> GetSuggestionAsync(
		string prompt,
		string context,
		CancellationToken cancellationToken = default)
		=> ChatAsync(
			Prompts.Suggestion(context),
			prompt,
			sanitize: true,
			cancellationToken);

	public Task<string> ExplainCommandAsync(
		string command,
		string context,
		CancellationToken cancellationToken = default)
		=> ChatAsync(
			Prompts.Explain(context),
			command,
			sanitize: false,
			cancellationToken);

	private async Task<string> ChatAsync(
		string system,
		string userMessage,
		bool sanitize,
		CancellationToken cancellationToken)
	{
		string text = (await client.CompleteAsync(
				new CompletionRequest(system, userMessage, MAX_TOKENS),
				cancellationToken))
			.Trim();

		return sanitize ? CommandSanitizer.Strip(text) : text;
	}
}
