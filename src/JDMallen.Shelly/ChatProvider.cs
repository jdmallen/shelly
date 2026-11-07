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
	int maxTokens = ChatProvider.DEFAULT_MAX_TOKENS) : IChatProvider
{
	internal const string
		ANTHROPIC_API_KEY_ENV_VAR = "ANTHROPIC_API_KEY_SHELLY";

	internal const string AZURE_API_KEY_ENV_VAR = "AZURE_OPENAI_API_KEY";
	internal const string AZURE_DEPLOYMENT_ENV_VAR = "AZURE_OPENAI_DEPLOYMENT";
	internal const string AZURE_ENDPOINT_ENV_VAR = "AZURE_OPENAI_ENDPOINT_HOST";
	internal const string OPEN_AI_API_KEY_ENV_VAR = "OPENAI_API_KEY_SHELLY";

	// ReSharper disable once MemberCanBePrivate.Global
	internal const int DEFAULT_MAX_TOKENS = 500;

	public Task<ChatReply> GetSuggestionAsync(
		string prompt,
		ShellContext context,
		CancellationToken cancellationToken = default)
		=> ChatAsync(
			Prompts.Suggestion(context),
			prompt,
			true,
			cancellationToken);

	public Task<ChatReply> ExplainCommandAsync(
		string command,
		ShellContext context,
		CancellationToken cancellationToken = default)
		=> ChatAsync(
			Prompts.Explain(context),
			command,
			false,
			cancellationToken);

	private async Task<ChatReply> ChatAsync(
		string system,
		string userMessage,
		bool sanitize,
		CancellationToken cancellationToken)
	{
		CompletionResult result = await client.GetCompletionAsync(
			new CompletionRequest(system, userMessage, maxTokens),
			cancellationToken);
		string text = result.Text.Trim();

		return new ChatReply(
			sanitize ? CommandSanitizer.Strip(text) : text,
			result.Usage);
	}
}
