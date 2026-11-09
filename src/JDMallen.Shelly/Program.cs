using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace JDMallen.Shelly;

public static class Program
{
	// Hosted providers answer in seconds; a self-hosted model may need minutes,
	// so its timeout comes from config instead.
	private const int HOSTED_TIMEOUT_SECONDS = 30;

	public static async Task<int> Main(string[] args)
	{
		switch (CommandLineArgs.Parse(args))
		{
			case CommandLineArgs.ShowHelp:
				Console.WriteLine(BuildHelpText());

				return 0;
			case CommandLineArgs.ShowVersion:
				Console.WriteLine(BuildVersionLine());

				return 0;
			case CommandLineArgs.ShowInfo:
				Console.WriteLine(BuildInfoReport());

				return 0;
			case CommandLineArgs.ShowConfig:
				if (!Console.IsInputRedirected)
				{
					return RunConfigWizard();
				}

				await Console.Error.WriteLineAsync(
					Strings.Error(Strings.InteractiveTerminalRequired));

				return 1;

			case CommandLineArgs.Error error:
				await Console.Error.WriteLineAsync(error.Message);
				await Console.Error.WriteLineAsync();
				await Console.Error.WriteLineAsync(BuildHelpText());

				return 1;

			// Piped input keeps going straight to the prompt, as before the menu.
			case CommandLineArgs.Run { Prompt: null }
				when !Console.IsInputRedirected:
				return await RunMainMenuAsync();
			case CommandLineArgs.Run run:
				return await RunAsync(run.Prompt);
			default:
				return 1;
		}
	}

	private static async Task<int> RunMainMenuAsync()
	{
		var io = new SystemConsoleIO();
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

			switch (MainMenu.Show(io, BuildVersionLine(), missingSettings))
			{
				case MainMenuChoice.Ask:
					io.WriteLine();

					return await RunAsync(null);
				case MainMenuChoice.Configure:
					io.WriteLine();
					new ConfigWizard().Run(true);
					io.WriteLine();

					continue;
				case MainMenuChoice.Info:
					io.WriteLine();
					io.WriteLine(BuildInfoReport());
					io.WriteLine();

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

		IChatProvider? provider = CreateProvider(config);
		if (provider is null)
		{
			return 1;
		}

		ShellContext context = ShellContext.Detect();
		var loop = new ReplLoop(provider, context, config.ShowTokenUsage);
		ReplResult result = await loop.RunAsync(prompt);

		// Before executing: the command may run long, or be interrupted along
		// with shelly, and the tokens were spent either way.
		RecordUsage(loop.SessionUsage);

		if (result.Action == ReplAction.Failed)
		{
			return 1;
		}

		if (string.IsNullOrWhiteSpace(result.Command))
		{
			return 0;
		}

		return result.Action switch
		{
			ReplAction.Execute => await ExecuteAsync(
				result.Command,
				context.Shell),
			ReplAction.Edit => WriteEditHandoff(result.Command),
			_               => 0,
		};
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
			Console.Error.WriteLine(Strings.UsageNotRecorded(path, error!));
		}
	}

	private static int WriteEditHandoff(string command)
	{
		string? path = EditHandoff.FilePath;
		if (string.IsNullOrEmpty(path))
		{
			// The edit action is only offered when the wrapper sets the env var,
			// so this is defensive: print the command so it isn't silently lost.
			Console.WriteLine(command);

			return 0;
		}

		File.WriteAllText(path, command);

		return 0;
	}

	// One REPL session, one client; the caller-owned lifetime spans the process.
	private static HttpClient CreateHttpClient(int timeoutSeconds) =>
		new()
		{
			Timeout = TimeSpan.FromSeconds(timeoutSeconds),
		};

	internal static IChatProvider? CreateProvider(ShellyConfig config)
		=> config.Provider.ToLowerInvariant() switch
		{
			ShellyConfig.AZURE_PROVIDER => CreateAzureProvider(config.Azure),
			ShellyConfig.ANTHROPIC_PROVIDER => CreateAnthropicProvider(
				config.Anthropic),
			ShellyConfig.OPENAI_PROVIDER
				or ShellyConfig.LOCAL_PROVIDER_ALIAS => CreateOpenAIProvider(
					config.OpenAI),
			_ => Fail(
				Strings.UnknownProvider(
					config.Provider,
					ShellyConfig.ConfigPath())),
		};

	private static IChatProvider? CreateAnthropicProvider(AnthropicConfig cfg)
	{
		string? apiKey =
			Environment.GetEnvironmentVariable(
				ChatProvider.ANTHROPIC_API_KEY_ENV_VAR);
		if (string.IsNullOrEmpty(apiKey))
		{
			return Fail(
				Strings.EnvVarNotSet(ChatProvider.ANTHROPIC_API_KEY_ENV_VAR));
		}

		return new ChatProvider(
			new AnthropicChatClient(
				CreateHttpClient(HOSTED_TIMEOUT_SECONDS),
				new AnthropicClientOptions(apiKey, cfg.Model)));
	}

	private static IChatProvider? CreateOpenAIProvider(OpenAIConfig cfg)
	{
		var missing = new List<string>();
		if (string.IsNullOrWhiteSpace(cfg.BaseUrl))
		{
			missing.Add("openai.baseUrl");
		}

		if (string.IsNullOrWhiteSpace(cfg.Model))
		{
			missing.Add("openai.model");
		}

		if (missing.Count > 0)
		{
			return Fail(
				Strings.OpenAIMisconfigured(
					string.Join($" {Strings.ListConjunction} ", missing),
					ShellyConfig.ConfigPath()));
		}

		if (cfg.TimeoutSeconds <= 0)
		{
			return Fail(
				Strings.MustBeGreaterThanZero(
					"openai.timeoutSeconds",
					cfg.TimeoutSeconds));
		}

		if (cfg.MaxTokens <= 0)
		{
			return Fail(
				Strings.MustBeGreaterThanZero(
					"openai.maxTokens",
					cfg.MaxTokens));
		}

		// Self-hosted runners usually accept anonymous requests, so the key is
		// optional: supply it only if the env var is set.
		string? apiKey =
			Environment.GetEnvironmentVariable(
				ChatProvider.OPEN_AI_API_KEY_ENV_VAR);

		return new ChatProvider(
			new OpenAICompatibleChatClient(
				CreateHttpClient(cfg.TimeoutSeconds),
				new OpenAICompatibleClientOptions(
					cfg.BaseUrl,
					cfg.Model,
					apiKey)),
			cfg.MaxTokens);
	}

	private static IChatProvider? CreateAzureProvider(AzureConfig cfg)
	{
		if (cfg.MaxTokens <= 0)
		{
			return Fail(
				Strings.MustBeGreaterThanZero(
					"azure.maxTokens",
					cfg.MaxTokens));
		}

		AzureSetting endpoint =
			cfg.ResolveEndpoint(Environment.GetEnvironmentVariable);
		AzureSetting deployment =
			cfg.ResolveDeployment(Environment.GetEnvironmentVariable);

		var missing = new List<string>();
		if (!endpoint.IsSet)
		{
			missing.Add("azure.endpoint");
		}

		if (!deployment.IsSet)
		{
			missing.Add("azure.deployment");
		}

		if (missing.Count > 0)
		{
			return Fail(
				Strings.AzureMisconfigured(
					string.Join($" {Strings.ListConjunction} ", missing),
					ShellyConfig.ConfigPath()));
		}

		WarnIfDeprecated(endpoint, "azure.endpoint");
		WarnIfDeprecated(deployment, "azure.deployment");

		AzureOpenAIClientOptions? options =
			CreateAzureOptions(cfg, endpoint.Value!, deployment.Value!);
		if (options is null)
		{
			return null;
		}

		return new ChatProvider(
			new AzureOpenAIChatClient(
				CreateHttpClient(HOSTED_TIMEOUT_SECONDS),
				options with { IsReasoningModel = cfg.ReasoningModel }),
			cfg.MaxTokens);
	}

	/// <summary>
	/// Client options for <see cref="AzureConfig.Auth" />: the API key from
	/// its environment variable, or an Entra ID token from the Azure CLI, which
	/// is fetched on the first request and cached until shortly before it
	/// expires. Null (after printing why) when the auth setup is incomplete.
	/// </summary>
	private static AzureOpenAIClientOptions? CreateAzureOptions(
		AzureConfig cfg,
		string endpoint,
		string deployment)
	{
		if (cfg.UsesAzureCli)
		{
			AzureCliTokenProvider? tokenProvider =
				cfg.TryCreateTokenProvider(out string? invalidKey);
			if (tokenProvider is not null)
			{
				return AzureOpenAIClientOptions.ForEntraId(
					endpoint,
					deployment,
					new CachingBearerTokenProvider(tokenProvider));
			}

			Fail(
				Strings.InvalidSetting(
					invalidKey!,
					ShellyConfig.ConfigPath()));

			return null;

		}

		if (!cfg.UsesApiKey)
		{
			Fail(Strings.UnknownAzureAuth(cfg.Auth, ShellyConfig.ConfigPath()));

			return null;
		}

		string? apiKey =
			Environment.GetEnvironmentVariable(
				ChatProvider.AZURE_API_KEY_ENV_VAR);
		if (!string.IsNullOrEmpty(apiKey))
		{
			return new AzureOpenAIClientOptions(apiKey, endpoint, deployment);
		}

		Fail(Strings.EnvVarNotSet(ChatProvider.AZURE_API_KEY_ENV_VAR));

		return null;

	}

	private static void WarnIfDeprecated(AzureSetting setting, string configKey)
	{
		if (setting.DeprecatedVariable is not null)
		{
			Console.Error.WriteLine(
				Strings.DeprecatedEnvVar(
					setting.DeprecatedVariable,
					configKey,
					ShellyConfig.ConfigPath()));
		}
	}

	private static IChatProvider? Fail(string message)
	{
		Console.Error.WriteLine(Strings.Error(message));

		return null;
	}

	// Runs in the shell the command was written for, i.e. the one shelly was
	// launched from, not $SHELL: a fish command fails under zsh.
	private static async Task<int> ExecuteAsync(string command, Shell shell)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = shell.Executable,
			UseShellExecute = false,
		};
		foreach (string flag in shell.CommandFlags)
		{
			startInfo.ArgumentList.Add(flag);
		}

		startInfo.ArgumentList.Add(command);

		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine(Strings.Executing);
		Console.ResetColor();

		Process? process;
		try
		{
			process = Process.Start(startInfo);
		}
		catch (Win32Exception)
		{
			// The shell isn't on PATH, e.g. a mistyped SHELLY_SHELL.
			process = null;
		}

		if (process is null)
		{
			await Console.Error.WriteLineAsync(
				Strings.Error(Strings.FailedToStartShell));

			return 1;
		}

		await process.WaitForExitAsync();

		return process.ExitCode;
	}
}
