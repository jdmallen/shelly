using JDMallen.Shelly;

namespace JDMallen.Shelly.Tests.Fakes;

internal sealed class FakeChatProvider : IChatProvider
{
	public Queue<string> SuggestionResponses { get; } = new();

	public Queue<string> ExplanationResponses { get; } = new();

	public List<string> SuggestionPrompts { get; } = [];

	public List<string> ExplanationCommands { get; } = [];

	public Exception? SuggestionThrow { get; set; }

	public Exception? ExplanationThrow { get; set; }

	public Task<string> GetSuggestionAsync(string prompt, string context, CancellationToken cancellationToken = default)
	{
		SuggestionPrompts.Add(prompt);
		if (SuggestionThrow is not null)
		{
			throw SuggestionThrow;
		}

		return Task.FromResult(SuggestionResponses.Dequeue());
	}

	public Task<string> ExplainCommandAsync(string command, string context, CancellationToken cancellationToken = default)
	{
		ExplanationCommands.Add(command);
		if (ExplanationThrow is not null)
		{
			throw ExplanationThrow;
		}

		return Task.FromResult(ExplanationResponses.Dequeue());
	}
}
