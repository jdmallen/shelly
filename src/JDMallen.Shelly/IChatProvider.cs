namespace JDMallen.Shelly;

internal interface IChatProvider
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
/// <param name="Truncated">
/// True when the model hit its token limit before finishing. The text is
/// then incomplete (a cut-off command can do something else entirely), so it
/// must not be shown as an answer.
/// </param>
internal readonly record struct ChatReply(
	string Text,
	TokenUsage? Usage = null,
	bool Truncated = false);
