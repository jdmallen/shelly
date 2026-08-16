namespace JDMallen.Shelly;

/// <summary>
/// The single <see cref="IChatProvider" /> implementation: owns shelly's prompt
/// construction, token budget, and answer post-processing, and delegates the
/// provider-specific wire formats to a JDMallen.Toolbox.AI
/// <see cref="IChatCompletionClient" />.
/// </summary>
/// <param name="client">The provider-specific wire client to delegate to.</param>
/// <param name="maxTokens">
/// The generation budget. The default suits the hosted providers, which answer
/// directly; reasoning models need considerably more, because the thinking block
/// is spent from the same budget and then discarded.
/// </param>
public sealed class ChatProvider(
	IChatCompletionClient client,
	int maxTokens = ChatProvider.DefaultMaxTokens) : IChatProvider
{
	internal const string AnthropicApiKeyEnvVar = "ANTHROPIC_API_KEY_SHELLY";
	internal const string AzureApiKeyEnvVar = "AZURE_OPENAI_API_KEY";
	internal const string AzureDeploymentEnvVar = "AZURE_OPENAI_DEPLOYMENT";
	internal const string AzureEndpointEnvVar = "AZURE_OPENAI_ENDPOINT_HOST";
	internal const string OpenAIApiKeyEnvVar = "OPENAI_API_KEY_SHELLY";

	// ReSharper disable once MemberCanBePrivate.Global
	internal const int DefaultMaxTokens = 500;

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
				new CompletionRequest(system, userMessage, maxTokens),
				cancellationToken))
			.Trim();

		return sanitize ? CommandSanitizer.Strip(text) : text;
	}
}
