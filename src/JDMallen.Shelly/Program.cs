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
				Console.WriteLine(
					InfoReport.Build(
						BuildVersionLine(),
						ShellyConfig.ConfigPath(),
						ShellyConfig.Load(),
						ShellDetector.Detect(),
						Environment.GetEnvironmentVariable));

				return 0;
			case CommandLineArgs.Error error:
				await Console.Error.WriteLineAsync(error.Message);
				await Console.Error.WriteLineAsync();
				await Console.Error.WriteLineAsync(BuildHelpText());

				return 1;
			case CommandLineArgs.Run run:
				return await RunAsync(run.Prompt);
			default:
				return 1;
		}
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
		ReplResult result = await new ReplLoop(provider, context).RunAsync(prompt);

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
			ReplAction.Execute => await ExecuteAsync(result.Command, context.Shell),
			ReplAction.Edit    => WriteEditHandoff(result.Command),
			_                  => 0,
		};
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
	{
		return config.Provider.ToLowerInvariant() switch
		{
			"azure"             => CreateAzureProvider(config.Azure),
			"anthropic"         => CreateAnthropicProvider(config.Anthropic),
			"openai" or "local" => CreateOpenAIProvider(config.OpenAI),
			_ => Fail(
				Strings.UnknownProvider(config.Provider, ShellyConfig.ConfigPath())),
		};
	}

	private static IChatProvider? CreateAnthropicProvider(AnthropicConfig cfg)
	{
		string? apiKey = Environment.GetEnvironmentVariable(ChatProvider.AnthropicApiKeyEnvVar);
		if (string.IsNullOrEmpty(apiKey))
		{
			return Fail(Strings.EnvVarNotSet(ChatProvider.AnthropicApiKeyEnvVar));
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
			return Fail(Strings.MustBeGreaterThanZero("openai.timeoutSeconds", cfg.TimeoutSeconds));
		}

		if (cfg.MaxTokens <= 0)
		{
			return Fail(Strings.MustBeGreaterThanZero("openai.maxTokens", cfg.MaxTokens));
		}

		// Self-hosted runners usually accept anonymous requests, so the key is
		// optional: supply it only if the env var is set.
		string? apiKey = Environment.GetEnvironmentVariable(ChatProvider.OpenAIApiKeyEnvVar);

		return new ChatProvider(
			new OpenAICompatibleChatClient(
				CreateHttpClient(cfg.TimeoutSeconds),
				new OpenAICompatibleClientOptions(cfg.BaseUrl, cfg.Model, apiKey)),
			cfg.MaxTokens);
	}

	private static IChatProvider? CreateAzureProvider(AzureConfig cfg)
	{
		if (cfg.MaxTokens <= 0)
		{
			return Fail(Strings.MustBeGreaterThanZero("azure.maxTokens", cfg.MaxTokens));
		}

		string? apiKey = Environment.GetEnvironmentVariable(ChatProvider.AzureApiKeyEnvVar);
		string? endpoint = Environment.GetEnvironmentVariable(ChatProvider.AzureEndpointEnvVar);
		string? deployment = Environment.GetEnvironmentVariable(ChatProvider.AzureDeploymentEnvVar);

		var missing = new List<string>();
		if (string.IsNullOrEmpty(apiKey))
		{
			missing.Add(ChatProvider.AzureApiKeyEnvVar);
		}

		if (string.IsNullOrEmpty(endpoint))
		{
			missing.Add(ChatProvider.AzureEndpointEnvVar);
		}

		if (string.IsNullOrEmpty(deployment))
		{
			missing.Add(ChatProvider.AzureDeploymentEnvVar);
		}

		if (missing.Count > 0)
		{
			return Fail(
				Strings.AzureMisconfigured(string.Join(", ", missing)));
		}

		return new ChatProvider(
			new AzureOpenAIChatClient(
				CreateHttpClient(HOSTED_TIMEOUT_SECONDS),
				new AzureOpenAIClientOptions(apiKey!, endpoint!, deployment!)),
			cfg.MaxTokens);
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
			await Console.Error.WriteLineAsync(Strings.Error(Strings.FailedToStartShell));

			return 1;
		}

		await process.WaitForExitAsync();

		return process.ExitCode;
	}
}
