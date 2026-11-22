using System.Reflection;

namespace JDMallen.Shelly;

internal static class Program
{
	private static readonly SystemConsoleIO IO = new();

	// CommandLineArgs is closed, so a new kind of argument that isn't handled
	// here is a compiler warning (CS8509), not a silent exit code.
	public static async Task<int> Main(string[] args) =>
		CommandLineArgs.Parse(args) switch
		{
			CommandLineArgs.ShowHelp => Print(BuildHelpText()),
			CommandLineArgs.ShowVersion => Print(BuildVersionLine()),
			CommandLineArgs.ShowInfo => Print(BuildInfoReport()),
			CommandLineArgs.ShowConfig => RunConfigCommand(),
			CommandLineArgs.Error error => ReportUsageError(error.Message),

			// Piped input keeps going straight to the prompt, as before the
			// menu.
			CommandLineArgs.Run { Prompt: null } when IO.IsInteractive
				=> await RunMainMenuAsync(),
			CommandLineArgs.Run run => await RunAsync(run.Prompt),
		};

	private static int Print(string text)
	{
		IO.WriteLine(text);

		return 0;
	}

	private static int RunConfigCommand()
	{
		if (IO.IsInteractive)
		{
			return RunConfigWizard();
		}

		IO.WriteError(Strings.Error(Strings.InteractiveTerminalRequired));

		return 1;
	}

	private static int ReportUsageError(string message)
	{
		IO.WriteError(message);
		IO.WriteError(string.Empty);
		IO.WriteError(BuildHelpText());

		return 1;
	}

	private static async Task<int> RunMainMenuAsync()
	{
		while (true)
		{
			// Re-checked on every pass, so the warning clears after Configure.
			ShellyConfig config = ShellyConfig.Load(
				ShellyConfig.ConfigPath(),
				out string? parseError);
			IReadOnlyList<string> missingSettings = ConfigCheck.FindMissing(
				config,
				parseError,
				Environment.GetEnvironmentVariable);

			switch (MainMenu.Show(IO, BuildVersionLine(), missingSettings))
			{
				case MainMenuChoice.Ask:
					IO.WriteLine();

					return await RunAsync(null);
				case MainMenuChoice.Configure:
					IO.WriteLine();
					new ConfigWizard(IO).Run(true);
					IO.WriteLine();

					continue;
				case MainMenuChoice.Info:
					IO.WriteLine();
					IO.WriteLine(BuildInfoReport());
					IO.WriteLine();

					continue;
				case MainMenuChoice.Exit:
				default:
					return 0;
			}
		}
	}

	private static int RunConfigWizard() =>
		new ConfigWizard(IO).Run() == ConfigWizardOutcome.Failed ? 1 : 0;

	private static string BuildInfoReport()
	{
		string usagePath = TokenUsageLog.DefaultPath("shelly");
		TokenUsageTotals? usage =
			TokenUsageLog.Read(usagePath, out string? usageError);

		return InfoReport.Build(
			BuildVersionLine(),
			ShellyConfig.ConfigPath(),
			ShellyConfig.Load(IO.WriteError),
			ShellDetector.Detect(),
			Environment.GetEnvironmentVariable,
			new InfoReport.UsageReport(usagePath, usage, usageError));
	}

	private static string BuildVersionLine()
	{
		string? version = typeof(Program)
			.Assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
			?.InformationalVersion;

		return $"shelly {version}";
	}

	private static string BuildHelpText()
	{
		string? copyright = typeof(Program)
			.Assembly
			.GetCustomAttribute<AssemblyCopyrightAttribute>()
			?.Copyright;

		return $"""
				{BuildVersionLine()}
				{copyright}

				  --help             {Strings.HelpOptionHelp}

				  --version          {Strings.HelpOptionVersion}

				  --info             {Strings.HelpOptionInfo}

				  --config           {Strings.HelpOptionConfig}

				  --                 {Strings.HelpOptionTerminator}

				  prompt (pos. 0)    {Strings.HelpPrompt}
				""";
	}

	private static async Task<int> RunAsync(string? prompt)
	{
		ShellyConfig config = ShellyConfig.Load(IO.WriteError);

		IChatProvider? provider = new ProviderFactory(
				Environment.GetEnvironmentVariable,
				IO.WriteError)
			.Create(config);
		if (provider is null)
		{
			return 1;
		}

		ShellContext context = ShellContext.Detect();
		var loop = new ReplLoop(
			provider,
			IO,
			context,
			config.ShowTokenUsage);

		return await new SessionRunner(
				loop,
				context.Shell,
				IO,
				new SystemInterruptSource(),
				new SystemCommandRunner(),
				RecordUsage,
				EditHandoff.FilePath)
			.RunAsync(prompt);
	}

	private static void RecordUsage(TokenUsageTally sessionUsage)
	{
		string path = TokenUsageLog.DefaultPath("shelly");
		if (!TokenUsageLog.TryRecord(
				path,
				sessionUsage,
				DateTimeOffset.Now,
				out string? error))
		{
			IO.WriteError(Strings.UsageNotRecorded(path, error!));
		}
	}
}
