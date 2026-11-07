namespace JDMallen.Shelly;

public interface IChatProvider
{
	Task<ChatReply> GetSuggestionAsync(
		string prompt,
		ShellContext context,
		CancellationToken cancellationToken = default);

	Task<ChatReply> ExplainCommandAsync(
		string command,
		ShellContext context,
		CancellationToken cancellationToken = default);
}

/// <summary>
/// A model answer and what it cost.
/// </summary>
/// <param name="Text">The answer, post-processed for display.</param>
/// <param name="Usage">
/// The request's token counts, or null when the provider reported none (some
/// self-hosted runners don't).
/// </param>
public readonly record struct ChatReply(string Text, TokenUsage? Usage = null);
