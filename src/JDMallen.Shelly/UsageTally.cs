namespace JDMallen.Shelly;

/// <summary>
/// Running token totals for one shelly run: every suggestion, retry and
/// explanation it asked the model for.
/// </summary>
public sealed class UsageTally
{
	/// <summary>Requests whose provider reported token usage.</summary>
	public int Requests { get; private set; }

	public long InputTokens { get; private set; }

	public long OutputTokens { get; private set; }

	public long TotalTokens => InputTokens + OutputTokens;

	/// <summary>
	/// Whether any request reported usage. False for a run that made no
	/// requests, or only made them to a provider that doesn't report usage.
	/// </summary>
	public bool HasUsage => Requests > 0;

	/// <summary>Adds one request's usage; a null usage is ignored.</summary>
	public void Add(TokenUsage? usage)
	{
		if (usage is null)
		{
			return;
		}

		Requests++;
		InputTokens += usage.InputTokens;
		OutputTokens += usage.OutputTokens;
	}
}
