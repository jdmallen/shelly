namespace JDMallen.Shelly;

public interface IChatProvider
{
	Task<string> GetSuggestionAsync(
		string prompt,
		string context,
		CancellationToken cancellationToken = default);

	Task<string> ExplainCommandAsync(
		string command,
		string context,
		CancellationToken cancellationToken = default);
}
