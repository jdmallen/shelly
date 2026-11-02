namespace JDMallen.Shelly;

public interface IChatProvider
{
	Task<string> GetSuggestionAsync(
		string prompt,
		ShellContext context,
		CancellationToken cancellationToken = default);

	Task<string> ExplainCommandAsync(
		string command,
		ShellContext context,
		CancellationToken cancellationToken = default);
}
