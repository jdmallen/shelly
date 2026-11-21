using JDMallen.Toolbox.AI.Azure;

namespace JDMallen.Shelly;

/// <summary>
/// Builds the chat provider a config names. When the config or environment
/// can't support it, writes why to <paramref name="writeError" /> and returns
/// null. <see cref="ConfigCheck" /> checks the same conditions for the main
/// menu, without printing.
/// </summary>
/// <param name="getEnvironmentVariable">
/// Reads API keys and deprecated settings.
/// </param>
/// <param name="writeError">Receives each error or warning line.</param>
internal sealed class ProviderFactory(
	Func<string, string?> getEnvironmentVariable,
	Action<string> writeError)
{
	// Hosted providers answer in seconds; a self-hosted model may need minutes,
	// so its timeout comes from config instead.
	private const int HOSTED_TIMEOUT_SECONDS = 30;

	private readonly string _configPath = ShellyConfig.ConfigPath();

	public IChatProvider? Create(ShellyConfig config) =>
		config.Kind switch
		{
			ProviderKind.Anthropic => CreateAnthropic(config.Anthropic),
			ProviderKind.OpenAI    => CreateOpenAI(config.OpenAI),
			ProviderKind.Azure     => CreateAzure(config.Azure),
			_ => Fail<IChatProvider>(
				Strings.UnknownProvider(config.Provider, _configPath)),
		};

	private IChatProvider? CreateAnthropic(AnthropicConfig cfg)
	{
		string? apiKey =
			getEnvironmentVariable(ChatProvider.ANTHROPIC_API_KEY_ENV_VAR);

		return string.IsNullOrEmpty(apiKey)
			? Fail<IChatProvider>(
				Strings.EnvVarNotSet(ChatProvider.ANTHROPIC_API_KEY_ENV_VAR))
			: new ChatProvider(
				new AnthropicChatClient(
					CreateHttpClient(HOSTED_TIMEOUT_SECONDS),
					new AnthropicClientOptions(apiKey, cfg.Model)));
	}

	private IChatProvider? CreateOpenAI(OpenAIConfig cfg)
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
			return Fail<IChatProvider>(
				Strings.OpenAIMisconfigured(
					string.Join($" {Strings.ListConjunction} ", missing),
					_configPath));
		}

		if (cfg.TimeoutSeconds <= 0)
		{
			return Fail<IChatProvider>(
				Strings.MustBeGreaterThanZero(
					"openai.timeoutSeconds",
					cfg.TimeoutSeconds));
		}

		if (cfg.MaxTokens <= 0)
		{
			return Fail<IChatProvider>(
				Strings.MustBeGreaterThanZero(
					"openai.maxTokens",
					cfg.MaxTokens));
		}

		// Self-hosted runners usually accept anonymous requests, so the key is
		// optional: supply it only if the env var is set.
		string? apiKey =
			getEnvironmentVariable(ChatProvider.OPEN_AI_API_KEY_ENV_VAR);

		return new ChatProvider(
			new OpenAICompatibleChatClient(
				CreateHttpClient(cfg.TimeoutSeconds),
				new OpenAICompatibleClientOptions(
					cfg.BaseUrl,
					cfg.Model,
					apiKey)),
			cfg.MaxTokens);
	}

	private IChatProvider? CreateAzure(AzureConfig cfg)
	{
		if (cfg.MaxTokens <= 0)
		{
			return Fail<IChatProvider>(
				Strings.MustBeGreaterThanZero(
					"azure.maxTokens",
					cfg.MaxTokens));
		}

		AzureSetting endpoint = cfg.ResolveEndpoint(getEnvironmentVariable);
		AzureSetting deployment = cfg.ResolveDeployment(getEnvironmentVariable);

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
			return Fail<IChatProvider>(
				Strings.AzureMisconfigured(
					string.Join($" {Strings.ListConjunction} ", missing),
					_configPath));
		}

		WarnIfDeprecated(endpoint, "azure.endpoint");
		WarnIfDeprecated(deployment, "azure.deployment");

		AzureOpenAIClientOptions? options =
			CreateAzureOptions(cfg, endpoint.Value!, deployment.Value!);

		return options is null
			? null
			: new ChatProvider(
				new AzureOpenAIChatClient(
					CreateHttpClient(HOSTED_TIMEOUT_SECONDS),
					options with { IsReasoningModel = cfg.ReasoningModel }),
				cfg.MaxTokens);
	}

	/// <summary>
	/// Client options for <see cref="AzureConfig.Auth" />: the API key from
	/// its environment variable, or an Entra ID token from the Azure CLI or
	/// Azure.Identity, which is fetched on the first request and cached until
	/// shortly before it expires. A browser or device code sign-in happens on
	/// that first request, and is remembered for later runs.
	/// </summary>
	private AzureOpenAIClientOptions? CreateAzureOptions(
		AzureConfig cfg,
		string endpoint,
		string deployment)
	{
		switch (cfg.AuthKind)
		{
			case AzureAuthKind.ApiKey:
				string? apiKey =
					getEnvironmentVariable(ChatProvider.AZURE_API_KEY_ENV_VAR);

				return string.IsNullOrEmpty(apiKey)
					? Fail<AzureOpenAIClientOptions>(
						Strings.EnvVarNotSet(
							ChatProvider.AZURE_API_KEY_ENV_VAR))
					: new AzureOpenAIClientOptions(
						apiKey,
						endpoint,
						deployment);

			case AzureAuthKind.AzureCli:
				AzureCliTokenProvider? cliTokenProvider =
					cfg.TryCreateTokenProvider(out string? invalidCliKey);

				return cliTokenProvider is null
					? Fail<AzureOpenAIClientOptions>(
						Strings.InvalidSetting(invalidCliKey!, _configPath))
					: AzureOpenAIClientOptions.ForEntraId(
						endpoint,
						deployment,
						new CachingBearerTokenProvider(cliTokenProvider));

			case AzureAuthKind.AzureIdentity:
				AzureCredentialOptions? credentialOptions =
					cfg.CredentialOptions(
						ShellyConfig.AuthenticationRecordPath(_configPath));
				string? invalidKey = credentialOptions is null
					? "azure.credential"
					: cfg.TryCreateTokenProvider(out string? invalidTokenKey)
						is null
						? invalidTokenKey
						: null;

				return invalidKey is not null
					? Fail<AzureOpenAIClientOptions>(
						Strings.InvalidSetting(invalidKey, _configPath))
					: AzureOpenAIClientOptions.ForEntraId(
						endpoint,
						deployment,
						AzureCredentialFactory.CreateTokenProvider(
							credentialOptions!,
							cfg.TokenProviderOptions().Scope));

			default:
				return Fail<AzureOpenAIClientOptions>(
					Strings.UnknownAzureAuth(cfg.Auth, _configPath));
		}
	}

	private void WarnIfDeprecated(AzureSetting setting, string configKey)
	{
		if (setting.DeprecatedVariable is not null)
		{
			writeError(
				Strings.DeprecatedEnvVar(
					setting.DeprecatedVariable,
					configKey,
					_configPath));
		}
	}

	private T? Fail<T>(string message)
		where T : class
	{
		writeError(Strings.Error(message));

		return null;
	}

	// One REPL session, one client; its lifetime spans the process.
	private static HttpClient CreateHttpClient(int timeoutSeconds) =>
		new() { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
}
