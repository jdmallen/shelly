using Anthropic;
using Anthropic.Models.Messages;

namespace JDMallen.Shelly;

public sealed class AnthropicChatProvider(string apiKey, string model) : IChatProvider
{
	internal const string ApiKeyEnvVar = "ANTHROPIC_API_KEY_SHELLY";

	private readonly AnthropicClient _client = new() { ApiKey = apiKey };

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
		var parameters = new MessageCreateParams
		{
			MaxTokens = 500,
			Model = model,
			System = system,
			Messages =
			[
				new MessageParam
				{
					Role = Role.User,
					Content = userMessage,
				},
			],
		};

		Message response = await _client.Messages.Create(
			parameters,
			cancellationToken: cancellationToken);

		string text = string.Concat(
				response.Content
					.Select(block => block.Value)
					.OfType<TextBlock>()
					.Select(t => t.Text))
			.Trim();

		return sanitize ? CommandSanitizer.Strip(text) : text;
	}
}
