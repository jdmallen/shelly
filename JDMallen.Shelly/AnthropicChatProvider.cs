using Anthropic;
using Anthropic.Models.Messages;

namespace JDMallen.Shelly;

public sealed class AnthropicChatProvider : IChatProvider
{
	private const string MODEL_ID = "claude-haiku-4-5-20251001";

	private readonly AnthropicClient _client = new()
	{
		ApiKey = Environment.GetEnvironmentVariable(Program.ApiKeyEnvVar),
	};

	public async Task<string> GetSuggestionAsync(
		string prompt,
		string context,
		CancellationToken cancellationToken = default)
	{
		var system
			= $"You are a shell command expert. Given a description of what the user wants to do, output ONLY the shell command(s) that accomplish it. No explanations, no markdown, no code fences---just the raw command(s). If multiple commands are needed, separate them with && or use appropriate shell syntax. Context: {context}";

		var parameters = new MessageCreateParams
		{
			MaxTokens = 500,
			Model = MODEL_ID,
			System = system,
			Messages =
			[
				new MessageParam
				{
					Role = Role.User,
					Content = prompt,
				},
			],
		};

		Message response = await _client.Messages.Create(
			parameters,
			cancellationToken: cancellationToken);

		return string.Concat(
				response.Content
					.Select(block => block.Value)
					.OfType<TextBlock>()
					.Select(text => text.Text)
			)
			.Trim();
	}
}
