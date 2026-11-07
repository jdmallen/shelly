using JDMallen.Toolbox.AI;

namespace JDMallen.Shelly.Tests.Fakes;

internal sealed class FakeChatProvider : IChatProvider
{
	public Queue<string> SuggestionResponses { get; } = new();

	public Queue<string> ExplanationResponses { get; } = new();

	/// <summary>
	/// Usage reported with each suggestion, in order; an empty queue reports
	/// none, like a provider that omits usage.
	/// </summary>
	public Queue<TokenUsage?> SuggestionUsages { get; } = new();

	public Queue<TokenUsage?> ExplanationUsages { get; } = new();

	public List<string> SuggestionPrompts { get; } = [];

	public List<string> ExplanationCommands { get; } = [];

	public Exception? SuggestionThrow { get; set; }

	public Exception? ExplanationThrow { get; set; }

	public Task<ChatReply> GetSuggestionAsync(
		string prompt,
		ShellContext context,
		CancellationToken cancellationToken = default)
	{
		SuggestionPrompts.Add(prompt);

		return SuggestionThrow is not null
			? throw SuggestionThrow
			: Task.FromResult(
				new ChatReply(
					SuggestionResponses.Dequeue(),
					SuggestionUsages.Count == 0
						? null
						: SuggestionUsages.Dequeue()));
	}

	public Task<ChatReply> ExplainCommandAsync(
		string command,
		ShellContext context,
		CancellationToken cancellationToken = default)
	{
		ExplanationCommands.Add(command);

		return ExplanationThrow is not null
			? throw ExplanationThrow
			: Task.FromResult(
				new ChatReply(
					ExplanationResponses.Dequeue(),
					ExplanationUsages.Count == 0
						? null
						: ExplanationUsages.Dequeue()));
	}
}
