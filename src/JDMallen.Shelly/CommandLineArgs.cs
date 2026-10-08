namespace JDMallen.Shelly;

/// <summary>
/// Outcome of parsing the command line. Pure data, so it needs no console.
/// </summary>
internal abstract record CommandLineArgs
{
	public sealed record ShowHelp : CommandLineArgs;

	public sealed record ShowVersion : CommandLineArgs;

	public sealed record Error(string Message) : CommandLineArgs;

	public sealed record Run(string? Prompt) : CommandLineArgs;

	private const string OPTION_TERMINATOR = "--";

	// Hand-written instead of a parsing library: one positional argument plus
	// two flags, and reflection-free parsing keeps Native AOT publish clean.
	public static CommandLineArgs Parse(string[] args)
	{
		var promptWords = new List<string>();
		var optionsEnded = false;

		foreach (string argument in args)
		{
			if (optionsEnded || !argument.StartsWith('-') || argument == "-")
			{
				promptWords.Add(argument);

				continue;
			}

			switch (argument)
			{
				case OPTION_TERMINATOR:
					optionsEnded = true;

					break;
				case "--help" or "-h" or "-?":
					return new ShowHelp();
				case "--version":
					return new ShowVersion();
				default:
					return new Error(
						$"Option '{argument.TrimStart('-')}' is unknown.");
			}
		}

		return new Run(
			promptWords.Count > 0 ? string.Join(' ', promptWords) : null);
	}
}
