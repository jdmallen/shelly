namespace JDMallen.Shelly;

internal enum MainMenuChoice
{
	Ask,
	Configure,
	Info,
	Exit,
}

/// <summary>
/// What a bare <c>shelly</c> shows on an interactive terminal: the version and
/// a numbered menu whose default is asking a question. Exit is 0 (or Esc).
/// </summary>
internal static class MainMenu
{
	private const int CONFIGURE_INDEX = 1;

	/// <param name="versionLine">The version line to display.</param>
	/// <param name="missingSettings">
	/// From <see cref="ConfigCheck.FindMissing" />: shown next to Configure
	/// when not empty.
	/// </param>
	/// <param name="io">The console I/O instance.</param>
	public static MainMenuChoice Show(
		IConsoleIO io,
		string versionLine,
		IReadOnlyList<string>? missingSettings = null)
	{
		io.Write($"{versionLine}\n", ConsoleColor.Green);
		io.WriteLine();

		Dictionary<int, string>? warnings = missingSettings is { Count: > 0 }
			? new Dictionary<int, string>
			{
				[CONFIGURE_INDEX] =
					Strings.MainMenuNeedsSetup(
						string.Join(", ", missingSettings)),
			}
			: null;

		int chosen = new ConsolePrompts(io).Choose(
			[
				Strings.MainMenuAsk,
				Strings.MainMenuConfigure,
				Strings.MainMenuInfo,
			],
			0,
			warnings,
			Strings.MainMenuExit);

		return chosen switch
		{
			0 => MainMenuChoice.Ask,
			CONFIGURE_INDEX => MainMenuChoice.Configure,
			2 => MainMenuChoice.Info,
			_ => MainMenuChoice.Exit,
		};
	}
}
