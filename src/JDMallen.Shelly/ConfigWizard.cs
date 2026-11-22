using System.Globalization;

using JDMallen.Toolbox.AI.Azure;

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
		new(
			ProviderKind.Anthropic,
			ShellyConfig.ANTHROPIC_PROVIDER,
			() => "Anthropic (Claude)"),
		new(
			ProviderKind.OpenAI,
			ShellyConfig.OPENAI_PROVIDER,
			()
				=> $"{Strings.ProviderOpenAICompatible} (openai.com, llama.cpp, Ollama, vLLM, ...)"),
		new(
			ProviderKind.Azure,
			ShellyConfig.AZURE_PROVIDER,
			() => "Azure OpenAI"),
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

	public ConfigWizard(IConsoleIO io)
		: this(
			io,
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
		_io.WriteLine(Strings.ConfigHeader(_configPath), ConsoleColor.Green);
		_io.WriteLine();

		ShellyConfig config = ShellyConfig.Load(
			_configPath,
			out string? parseError);
		if (parseError is not null)
		{
			_io.WriteLine(
				Strings.ConfigUnreadable(_configPath, parseError),
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
			CurrentProviderIndex(config.Kind),
			zeroOption: fromMainMenu
				? Strings.MenuBackToMainMenu
				: Strings.MainMenuExit);
		if (chosen == ConsolePrompts.CANCELLED)
		{
			// Nothing has been changed yet, so there's nothing to report.
			return ConfigWizardOutcome.Discarded;
		}

		ProviderKind provider = Providers[chosen].Kind;

		// Keep the "local" alias if that's what the user already had.
		if (provider != ProviderKind.OpenAI
			|| config.Kind != ProviderKind.OpenAI)
		{
			config.Provider = Providers[chosen].Name;
		}

		_io.WriteLine();
		_io.WriteLine(Strings.SettingsHeader);
		string? apiKeyVariable = provider switch
		{
			ProviderKind.Azure  => ConfigureAzure(config.Azure),
			ProviderKind.OpenAI => ConfigureOpenAI(config.OpenAI),
			_                   => ConfigureAnthropic(config.Anthropic),
		};

		_io.WriteLine();
		string? pendingApiKey = null;
		switch (apiKeyVariable)
		{
			case null
				when config.Azure.AuthKind == AzureAuthKind.AzureIdentity:
				WriteAzureIdentitySignIn();

				break;
			case null:
				WriteAzureCliSignIn(config.Azure);

				break;
			default:
				pendingApiKey = ConfigureApiKey(
					apiKeyVariable,
					provider == ProviderKind.OpenAI);

				break;
		}

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

	/// <returns>
	/// The API key variable, or null when the chosen auth needs no key.
	/// </returns>
	private string? ConfigureAzure(AzureConfig azure)
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

		_io.WriteLine();
		_io.WriteLine(Strings.AzureAuthHeader);
		AzureAuthKind? currentKind = azure.AuthKind;
		int current = Array.FindIndex(
			AzureConfig.AuthMethods,
			method => method.Kind == currentKind);
		int chosen = _prompts.Choose(
			[
				Strings.AzureAuthApiKey(ChatProvider.AZURE_API_KEY_ENV_VAR),
				Strings.AzureAuthAzureCli,
				Strings.AzureAuthAzureIdentity,
			],
			Math.Max(current, 0));

		// Esc keeps the current method; an unrecognized one becomes the key.
		if (chosen != ConsolePrompts.CANCELLED || current < 0)
		{
			azure.Auth = AzureConfig.AuthMethods[Math.Max(chosen, 0)].Name;
		}

		switch (azure.AuthKind)
		{
			case AzureAuthKind.AzureIdentity:
				ConfigureAzureCredential(azure);

				break;
			case AzureAuthKind.ApiKey:
				return ChatProvider.AZURE_API_KEY_ENV_VAR;
		}

		if (azure.UsesInteractiveSignIn)
		{
			_io.WriteLine();
			_io.WriteLine(Strings.AzureIdentityTenantHint);
		}

		azure.TenantId = _prompts.OptionalText(
			"azure.tenantId",
			azure.TenantId,
			value => IsInvalidTenant(value)
				? Strings.InvalidTenantId
				: null);
		azure.TokenScope = _prompts.OptionalText(
			"azure.tokenScope",
			azure.TokenScope,
			value => IsInvalidScope(value)
				? Strings.InvalidTokenScope(AzureOpenAIScopes.DEFAULT)
				: null);

		return null;
	}

	/// <summary>
	/// Which Azure.Identity sign-in to use. The choices are the config values
	/// themselves, so they read the same in every language.
	/// </summary>
	private void ConfigureAzureCredential(AzureConfig azure)
	{
		_io.WriteLine();
		_io.WriteLine(Strings.AzureCredentialHeader);
		AzureCredentialKind? currentKind = azure.CredentialKind;
		int current = Array.FindIndex(
			AzureConfig.Credentials,
			entry => entry.Kind == currentKind);
		int chosen = _prompts.Choose(
			[.. AzureConfig.Credentials.Select(entry => entry.Name)],
			Math.Max(current, 0));

		// Esc keeps the current credential; an unrecognized one becomes the
		// browser sign-in.
		if (chosen != ConsolePrompts.CANCELLED || current < 0)
		{
			azure.Credential =
				AzureConfig.Credentials[Math.Max(chosen, 0)].Name;
		}
	}

	private static bool IsInvalidScope(string tokenScope) =>
		new AzureConfig { TokenScope = tokenScope }.InvalidTokenSetting()
		== "azure.tokenScope";

	// Each value is checked alone: checked with the saved scope, a bad scope
	// was reported first and let any tenant through.
	private static bool IsInvalidTenant(string tenantId) =>
		new AzureConfig { TenantId = tenantId }.InvalidTokenSetting()
		== "azure.tenantId";

	/// <summary>
	/// The Azure CLI counterpart of the API key step: whether <c>az</c> is on
	/// PATH, the sign-in command, and the role the account needs. shelly
	/// never runs <c>az login</c> itself (it's interactive).
	/// </summary>
	private void WriteAzureCliSignIn(AzureConfig azure)
	{
		_io.WriteLine(Strings.AzureCliSignInHeader);
		string? azPath = AzureCli.Find(_environment.Get);
		_io.WriteLine(
			$"  {(azPath is null ? Strings.AzureCliNotFound(AzureCli.INSTALL_URL) : Strings.AzureCliFound(azPath))}",
			azPath is null ? ConsoleColor.Yellow : ConsoleColor.Green);
		_io.WriteLine($"  {Strings.AzureCliSignIn}");
		_io.WriteLine();
		_io.WriteLine(
			$"    {AzureCli.SignInCommand(azure)}",
			ConsoleColor.Yellow);
		_io.WriteLine();
		_io.WriteLine(
			$"  {Strings.AzureCliRoleNote(AzureCli.DATA_PLANE_ROLE)}");
	}

	/// <summary>
	/// The Azure.Identity counterpart of the API key step: when the sign-in
	/// happens, and the role the account needs.
	/// </summary>
	private void WriteAzureIdentitySignIn()
	{
		_io.WriteLine(Strings.AzureIdentitySignInHeader);
		_io.WriteLine($"  {Strings.AzureIdentitySignInNote}");
		_io.WriteLine();
		_io.WriteLine(
			$"  {Strings.AzureCliRoleNote(AzureCli.DATA_PLANE_ROLE)}");
	}

	/// <returns>
	/// A key typed on Windows, to persist only once the user saves; else null.
	/// </returns>
	private string? ConfigureApiKey(string variable, bool optional)
	{
		bool isSet = !string.IsNullOrEmpty(_environment.Get(variable));
		_io.WriteLine(Strings.ApiKeyHeader);
		_io.WriteLine(
			$"  {(isSet ? Strings.ApiKeySet(variable) : Strings.ApiKeyNotSet(variable))}",
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
		_io.WriteLine($"    {line}", ConsoleColor.Yellow);
	}

	/// <summary>
	/// <see cref="Shell.SetVariableLine" /> with the key placeholder; a shell
	/// with no convention of its own gets a POSIX <c>export</c> line.
	/// </summary>
	internal static (string Line, string? StartupFile) StartupLine(
		Shell shell,
		string variable)
	{
		const string value = $"'{KEY_PLACEHOLDER}'";

		return shell.SetVariableLine(variable, value)
			?? ($"export {variable}={value}", Strings.UnknownShellStartupFile);
	}

	private static int CurrentProviderIndex(ProviderKind? provider) =>
		Math.Max(Array.FindIndex(Providers, option => option.Kind == provider), 0);

	// The key waits for the config save, so declining "Save changes?" (or a
	// failed write) leaves the user's environment untouched too.
	private ConfigWizardOutcome Save(
		ShellyConfig config,
		bool backUpFirst,
		string? apiKeyVariable,
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
			_io.WriteLine(
				Strings.Error(Strings.ConfigSaveFailed(_configPath, ex.Message)),
				ConsoleColor.Red);

			return ConfigWizardOutcome.Failed;
		}

		_io.WriteLine(Strings.ConfigSaved(_configPath), ConsoleColor.Green);
		if (pendingApiKey is null)
		{
			return ConfigWizardOutcome.Saved;
		}

		_environment.SetPersistent(apiKeyVariable!, pendingApiKey);
		_io.WriteLine(
			Strings.UserVariableSaved(apiKeyVariable!),
			ConsoleColor.Green);

		return ConfigWizardOutcome.Saved;
	}

	private ConfigWizardOutcome Discard()
	{
		_io.WriteLine(Strings.ConfigNotSaved);

		return ConfigWizardOutcome.Discarded;
	}

	// The label is looked up when shown, so it follows CurrentUICulture.
	private sealed record ProviderOption(
		ProviderKind Kind,
		string Name,
		Func<string> Label);
}
