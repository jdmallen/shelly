using System.Reflection;

namespace JDMallen.Shelly;

internal static class Program
{
	private static readonly IConsoleIO Io = new SystemConsoleIO();

	public static async Task<int> Main(string[] args)
	{
		switch (CommandLineArgs.Parse(args))
		{
			case CommandLineArgs.ShowHelp:
				Io.WriteLine(BuildHelpText());

				return 0;
			case CommandLineArgs.ShowVersion:
				Io.WriteLine(BuildVersionLine());

				return 0;
			case CommandLineArgs.ShowInfo:
				Io.WriteLine(BuildInfoReport());

				return 0;
			case CommandLineArgs.ShowConfig:
				if (Io.IsInteractive)
				{
					return RunConfigWizard();
				}

				Io.WriteError(
					Strings.Error(Strings.InteractiveTerminalRequired));

				return 1;

			case CommandLineArgs.Error error:
				Io.WriteError(error.Message);
				Io.WriteError(string.Empty);
				Io.WriteError(BuildHelpText());

				return 1;

			// Piped input keeps going straight to the prompt, as before the menu.
			case CommandLineArgs.Run { Prompt: null }
				when Io.IsInteractive:
				return await RunMainMenuAsync();
			case CommandLineArgs.Run run:
				return await RunAsync(run.Prompt);
			default:
				return 1;
		}
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

			switch (MainMenu.Show(Io, BuildVersionLine(), missingSettings))
			{
				case MainMenuChoice.Ask:
					Io.WriteLine();

					return await RunAsync(null);
				case MainMenuChoice.Configure:
					Io.WriteLine();
					new ConfigWizard().Run(true);
					Io.WriteLine();

					continue;
				case MainMenuChoice.Info:
					Io.WriteLine();
					Io.WriteLine(BuildInfoReport());
					Io.WriteLine();

					continue;
				case MainMenuChoice.Exit:
				default:
					return 0;
			}
		}
	}

	private static int RunConfigWizard() =>
		new ConfigWizard().Run() == ConfigWizardOutcome.Failed ? 1 : 0;

	private static string BuildInfoReport()
	{
		string usagePath = TokenUsageLog.DefaultPath("shelly");
		TokenUsageTotals? usage =
			TokenUsageLog.Read(usagePath, out string? usageError);

		return InfoReport.Build(
			BuildVersionLine(),
			ShellyConfig.ConfigPath(),
			ShellyConfig.Load(),
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
		ShellyConfig config = ShellyConfig.Load();

		IChatProvider? provider = new ProviderFactory(
				Environment.GetEnvironmentVariable,
				Io.WriteError)
			.Create(config);
		if (provider is null)
		{
			return 1;
		}

		ShellContext context = ShellContext.Detect();
		var loop = new ReplLoop(provider, context, config.ShowTokenUsage);

		return await new SessionRunner(
				loop,
				context.Shell,
				Io,
				new SystemInterruptSource(),
				new SystemCommandRunner(),
				RecordUsage)
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
			Io.WriteError(Strings.UsageNotRecorded(path, error!));
		}
	}
}
