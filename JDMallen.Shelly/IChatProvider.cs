namespace JDMallen.Shelly;

public interface IChatProvider
{
	Task<string> GetSuggestionAsync(
		string prompt,
		string context,
		CancellationToken cancellationToken = default);
}
