using System.Globalization;

namespace JDMallen.Shelly;

internal enum ConfigWizardOutcome
{
	Saved,
	Discarded,

	/// <summary>The file couldn't be written; exit non-zero.</summary>
	Failed,
}

/// <summary>
/// The <c>--config</c> wizard: pick a provider, fill in its settings, get its
/// API key in place, then save <c>config.json</c>. The key itself never goes
/// in the file: on Windows it can be saved as a user environment variable;
/// elsewhere the wizard prints the line to add to the shell's startup file,
/// because shelly doesn't edit those.
/// </summary>
internal sealed class ConfigWizard
{
	private const string KEY_PLACEHOLDER = "<your-key>";

	private const string BACKUP_TIMESTAMP_FORMAT = "yyyyMMdd'T'HHmmss'Z'";

	private static readonly ProviderOption[] Providers =
	[
		new(ShellyConfig.ANTHROPIC_PROVIDER, () => "Anthropic (Claude)"),
		new(
			ShellyConfig.OPENAI_PROVIDER,
			()
				=> $"{Strings.ProviderOpenAICompatible} (openai.com, llama.cpp, Ollama, vLLM, ...)"),
		new(ShellyConfig.AZURE_PROVIDER, () => "Azure OpenAI"),
	];

	private readonly IConsoleIO _io;
	private readonly ConsolePrompts _prompts;
	private readonly IUserEnvironment _environment;
	private readonly Shell _shell;
	private readonly string _configPath;
	private readonly Func<DateTime> _utcNow;

	internal ConfigWizard(
		IConsoleIO io,
		IUserEnvironment environment,
		Shell shell,
		string configPath,
		Func<DateTime> utcNow)
	{
		_io = io;
		_prompts = new ConsolePrompts(io);
		_environment = environment;
		_shell = shell;
		_configPath = configPath;
		_utcNow = utcNow;
	}

	public ConfigWizard()
		: this(
			new SystemConsoleIO(),
			new SystemUserEnvironment(),
			ShellDetector.Detect(),
			ShellyConfig.ConfigPath(),
			() => DateTime.UtcNow)
	{
	}

	/// <param name="fromMainMenu">
	/// Labels the provider menu's 0 option "Back to main menu" instead of
	/// "Exit". Either way, 0 or Esc there leaves before anything changes.
	/// </param>
	public ConfigWizardOutcome Run(bool fromMainMenu = false)
	{
		_io.Write($"{Strings.ConfigHeader(_configPath)}\n", ConsoleColor.Green);
		_io.WriteLine();

		ShellyConfig config = ShellyConfig.Load(
			_configPath,
			out string? parseError);
		if (parseError is not null)
		{
			_io.Write(
				$"{Strings.ConfigUnreadable(_configPath, parseError)}\n",
				ConsoleColor.Yellow);
			if (!_prompts.YesNo(Strings.ContinuePrompt, false))
			{
				return Discard();
			}

			_io.WriteLine();
		}

		_io.WriteLine(Strings.ProviderHeader);
		int chosen = _prompts.Choose(
			[.. Providers.Select(provider => provider.Label())],
			CurrentProviderIndex(config.Provider),
			zeroOption: fromMainMenu
				? Strings.MenuBackToMainMenu
				: Strings.MainMenuExit);
		if (chosen == ConsolePrompts.CANCELLED)
		{
			// Nothing has been changed yet, so there's nothing to report.
			return ConfigWizardOutcome.Discarded;
		}

		string provider = Providers[chosen].Name;

		// Keep the "local" alias if that's what the user already had.
		if (provider != ShellyConfig.OPENAI_PROVIDER
			|| !ShellyConfig.IsOpenAIProvider(config.Provider))
		{
			config.Provider = provider;
		}

		_io.WriteLine();
		_io.WriteLine(Strings.SettingsHeader);
		string apiKeyVariable = provider switch
		{
			ShellyConfig.AZURE_PROVIDER => ConfigureAzure(config.Azure),
			ShellyConfig.OPENAI_PROVIDER => ConfigureOpenAI(config.OpenAI),
			_ => ConfigureAnthropic(config.Anthropic),
		};

		_io.WriteLine();
		string? pendingApiKey = ConfigureApiKey(
			apiKeyVariable,
			provider == ShellyConfig.OPENAI_PROVIDER);

		_io.WriteLine();

		return !_prompts.YesNo(Strings.SaveChangesPrompt, true)
			? Discard()
			: Save(
				config,
				parseError is not null,
				apiKeyVariable,
				pendingApiKey);
	}

	private string ConfigureAnthropic(AnthropicConfig anthropic)
	{
		anthropic.Model = _prompts.Text("anthropic.model", anthropic.Model);

		return ChatProvider.ANTHROPIC_API_KEY_ENV_VAR;
	}

	private string ConfigureOpenAI(OpenAIConfig openAI)
	{
		openAI.BaseUrl = _prompts.HttpUrl("openai.baseUrl", openAI.BaseUrl);
		openAI.Model = _prompts.Text("openai.model", openAI.Model);
		openAI.TimeoutSeconds = _prompts.PositiveInteger(
			"openai.timeoutSeconds",
			openAI.TimeoutSeconds);
		openAI.MaxTokens = _prompts.PositiveInteger(
			"openai.maxTokens",
			openAI.MaxTokens);

		return ChatProvider.OPEN_AI_API_KEY_ENV_VAR;
	}

	private string ConfigureAzure(AzureConfig azure)
	{
		// Offer the deprecated variables' values, so saving once moves an
		// env-only setup into the config file.
		azure.Endpoint = _prompts.HttpUrl(
			"azure.endpoint",
			azure.ResolveEndpoint(_environment.Get).Value ?? string.Empty);
		azure.Deployment = _prompts.Text(
			"azure.deployment",
			azure.ResolveDeployment(_environment.Get).Value ?? string.Empty);
		azure.ReasoningModel = _prompts.Boolean(
			"azure.reasoningModel",
			azure.ReasoningModel);
		azure.MaxTokens = _prompts.PositiveInteger(
			"azure.maxTokens",
			azure.MaxTokens);

		return ChatProvider.AZURE_API_KEY_ENV_VAR;
	}

	/// <returns>
	/// A key typed on Windows, to persist only once the user saves; else null.
	/// </returns>
	private string? ConfigureApiKey(string variable, bool optional)
	{
		bool isSet = !string.IsNullOrEmpty(_environment.Get(variable));
		_io.WriteLine(Strings.ApiKeyHeader);
		_io.Write(
			$"  {(isSet ? Strings.ApiKeySet(variable) : Strings.ApiKeyNotSet(variable))}\n",
			isSet ? ConsoleColor.Green : ConsoleColor.Yellow);
		if (optional)
		{
			_io.WriteLine($"  {Strings.ApiKeyOptional}");
		}

		if (_environment.CanPersist)
		{
			return AskForKeyToPersist(
				variable,
				!isSet && !optional);
		}

		if (!isSet)
		{
			WriteStartupInstructions(variable);
		}

		return null;
	}

	private string? AskForKeyToPersist(string variable, bool defaultYes)
	{
		if (!_prompts.YesNo(
				Strings.SetUserVariablePrompt(variable),
				defaultYes,
				true))
		{
			return null;
		}

		string? key = _prompts.Secret(Strings.EnterApiKey);
		if (!string.IsNullOrWhiteSpace(key))
		{
			return key.Trim();
		}

		_io.WriteLine($"  {Strings.Skipped}");

		return null;
	}

	private void WriteStartupInstructions(string variable)
	{
		(string line, string? startupFile) = StartupLine(_shell, variable);
		_io.WriteLine(
			startupFile is null
				? $"  {Strings.FishUniversalVariable(KEY_PLACEHOLDER)}"
				: $"  {Strings.ShellStartupLine(startupFile, KEY_PLACEHOLDER)}");
		_io.WriteLine();
		_io.Write($"    {line}\n", ConsoleColor.Yellow);
	}

	/// <summary>
	/// The line that sets <paramref name="variable" /> in
	/// <paramref name="shell" />, and the startup file it belongs in; a null
	/// file means the line is run once instead (fish's universal variables
	/// persist on their own).
	/// </summary>
	internal static (string Line, string? StartupFile) StartupLine(
		Shell shell,
		string variable)
	{
		const string value = $"'{KEY_PLACEHOLDER}'";

		return shell.Name switch
		{
			"fish" => ($"set -Ux {variable} {value}", null),
			"zsh" => ($"export {variable}={value}", "~/.zshrc"),
			"bash" => ($"export {variable}={value}", "~/.bashrc"),
			"tcsh" => ($"setenv {variable} {value}", "~/.tcshrc"),
			"csh" => ($"setenv {variable} {value}", "~/.cshrc"),
			"pwsh" or "powershell" => ($"$env:{variable} = {value}",
				"$PROFILE"),
			"nu" => ($"$env.{variable} = {value}", "$nu.env-path"),
			"elvish" => ($"set E:{variable} = {value}",
				"~/.config/elvish/rc.elv"),
			"ksh" or "ksh93" or "mksh" or "lksh" or "oksh" or "pdksh"
				=> ($"export {variable}={value}", "~/.kshrc"),
			"sh" or "dash" or "ash" or "posh" or "yash"
				=> ($"export {variable}={value}", "~/.profile"),
			_ => ($"export {variable}={value}",
				Strings.UnknownShellStartupFile),
		};
	}

	private static int CurrentProviderIndex(string provider)
	{
		if (ShellyConfig.IsOpenAIProvider(provider))
		{
			return Array.FindIndex(
				Providers,
				option => option.Name == ShellyConfig.OPENAI_PROVIDER);
		}

		int index = Array.FindIndex(
			Providers,
			option => option.Name.Equals(
				provider,
				StringComparison.OrdinalIgnoreCase));

		return Math.Max(index, 0);
	}

	// The key waits for the config save, so declining "Save changes?" (or a
	// failed write) leaves the user's environment untouched too.
	private ConfigWizardOutcome Save(
		ShellyConfig config,
		bool backUpFirst,
		string apiKeyVariable,
		string? pendingApiKey)
	{
		try
		{
			if (backUpFirst && File.Exists(_configPath))
			{
				var timestamp = _utcNow()
					.ToString(
						BACKUP_TIMESTAMP_FORMAT,
						CultureInfo.InvariantCulture);
				var backupPath = $"{_configPath}.bak_{timestamp}";
				File.Copy(_configPath, backupPath, false);
				_io.WriteLine(Strings.ConfigBackedUp(backupPath));
			}

			config.Save(_configPath);
		}
		catch (Exception ex) when (ex is IOException
									   or UnauthorizedAccessException)
		{
			_io.Write(
				$"{Strings.Error(Strings.ConfigSaveFailed(_configPath, ex.Message))}\n",
				ConsoleColor.Red);

			return ConfigWizardOutcome.Failed;
		}

		_io.Write($"{Strings.ConfigSaved(_configPath)}\n", ConsoleColor.Green);
		if (pendingApiKey is null)
		{
			return ConfigWizardOutcome.Saved;
		}

		_environment.SetPersistent(apiKeyVariable, pendingApiKey);
		_io.Write(
			$"{Strings.UserVariableSaved(apiKeyVariable)}\n",
			ConsoleColor.Green);

		return ConfigWizardOutcome.Saved;
	}

	private ConfigWizardOutcome Discard()
	{
		_io.WriteLine(Strings.ConfigNotSaved);

		return ConfigWizardOutcome.Discarded;
	}

	// The label is looked up when shown, so it follows CurrentUICulture.
	private sealed record ProviderOption(string Name, Func<string> Label);
}
