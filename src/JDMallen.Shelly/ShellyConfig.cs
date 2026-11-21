using System.Text.Json;
using System.Text.Json.Serialization;

using JDMallen.Toolbox.AI.Azure;

namespace JDMallen.Shelly;

/// <summary>The providers <see cref="ShellyConfig.Provider" /> can name.</summary>
internal enum ProviderKind
{
	Anthropic,

	/// <summary>Any OpenAI-compatible server, named "openai" or "local".</summary>
	OpenAI,
	Azure,
}

/// <summary>The methods <see cref="AzureConfig.Auth" /> can name.</summary>
internal enum AzureAuthKind
{
	ApiKey,
	AzureCli,
	AzureIdentity,
}

internal sealed class ShellyConfig
{
	internal const string ANTHROPIC_PROVIDER = "anthropic";
	internal const string AZURE_PROVIDER = "azure";
	internal const string OPENAI_PROVIDER = "openai";

	/// <summary>Accepted in place of <see cref="OPENAI_PROVIDER" />.</summary>
	internal const string LOCAL_PROVIDER_ALIAS = "local";

	public string Provider { get; set; } = ANTHROPIC_PROVIDER;

	/// <summary>
	/// The provider <see cref="Provider" /> names, ignoring case; null when it
	/// names none.
	/// </summary>
	internal ProviderKind? Kind =>
		Provider.ToLowerInvariant() switch
		{
			ANTHROPIC_PROVIDER => ProviderKind.Anthropic,
			OPENAI_PROVIDER or LOCAL_PROVIDER_ALIAS => ProviderKind.OpenAI,
			AZURE_PROVIDER => ProviderKind.Azure,
			_ => null,
		};

	public AnthropicConfig Anthropic { get; set; } = new();

	public AzureConfig Azure { get; set; } = new();

	[JsonPropertyName("openai")]
	public OpenAIConfig OpenAI { get; set; } = new();

	/// <summary>
	/// Whether the REPL shows the run's token count after the menu options.
	/// Usage is still added to the lifetime totals when this is off.
	/// </summary>
	public bool ShowTokenUsage { get; set; } = true;

	public static ShellyConfig Load()
	{
		string path = ConfigPath();
		ShellyConfig config = Load(path, out string? parseError);
		if (parseError is not null)
		{
			Console.Error.WriteLine(
				Strings.ConfigParseWarning(path, parseError));
		}

		return config;
	}

	/// <summary>
	/// Loads <paramref name="path" />, writing a default file first if there is
	/// none. A file that fails to parse yields the defaults, with the parser's
	/// message in <paramref name="parseError" />, so a caller about to save can
	/// tell the defaults apart from the user's settings.
	/// </summary>
	internal static ShellyConfig Load(string path, out string? parseError)
	{
		parseError = null;
		if (!File.Exists(path))
		{
			var defaults = new ShellyConfig();
			TryWriteDefaults(path, defaults);

			return defaults;
		}

		try
		{
			using FileStream stream = File.OpenRead(path);
			ShellyConfig? loaded = JsonSerializer.Deserialize(
				stream,
				ShellyConfigJsonContext.Default.ShellyConfig);

			return loaded ?? new ShellyConfig();
		}
		// JsonException also covers a null where a value is required (see
		// RespectNullableAnnotations below); an unreadable file is reported
		// the same way rather than crashing.
		catch (Exception ex) when (ex is JsonException
									   or IOException
									   or UnauthorizedAccessException)
		{
			parseError = ex.Message;

			return new ShellyConfig();
		}
	}

	/// <summary>
	/// Writes the config to <paramref name="path" /> via a temporary file in
	/// the same directory, so an interrupted write can't leave a truncated
	/// config behind.
	/// </summary>
	internal void Save(string path)
	{
		string directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
		Directory.CreateDirectory(directory);
		string temporaryPath = Path.Combine(
			directory,
			$".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

		try
		{
			using (FileStream stream = File.Create(temporaryPath))
			{
				JsonSerializer.Serialize(
					stream,
					this,
					ShellyConfigJsonContext.Default.ShellyConfig);
			}

			File.Move(temporaryPath, path, true);
		}
		finally
		{
			File.Delete(temporaryPath);
		}
	}

	/// <summary>
	/// Where an Azure browser or device code sign-in is remembered (account
	/// and tenant ids; the tokens stay in the system keyring): next to
	/// <paramref name="configPath" />.
	/// </summary>
	internal static string AuthenticationRecordPath(string configPath) =>
		Path.Combine(
			Path.GetDirectoryName(Path.GetFullPath(configPath))!,
			"auth-record.json");

	public static string ConfigPath()
	{
		string baseDir =
			Environment.GetFolderPath(
				Environment.SpecialFolder.ApplicationData);
		if (!string.IsNullOrEmpty(baseDir))
		{
			return Path.Combine(baseDir, "shelly", "config.json");
		}

		string? home = Environment.GetEnvironmentVariable("HOME");
		baseDir = string.IsNullOrEmpty(home)
			? "."
			: Path.Combine(home, ".config");

		return Path.Combine(baseDir, "shelly", "config.json");
	}

	private static void TryWriteDefaults(string path, ShellyConfig defaults)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			using FileStream stream = File.Create(path);
			JsonSerializer.Serialize(
				stream,
				defaults,
				ShellyConfigJsonContext.Default.ShellyConfig);
		}
		catch (IOException)
		{
			// Non-fatal — defaults remain in-memory.
		}
		catch (UnauthorizedAccessException)
		{
			// Non-fatal — defaults remain in-memory.
		}
	}
}

internal sealed class AnthropicConfig
{
	public string Model { get; set; } = "claude-haiku-4-5-20251001";
}

/// <summary>
/// Azure OpenAI settings that aren't secrets. Only the API key stays in an
/// environment variable.
/// </summary>
internal sealed class AzureConfig
{
	/// <summary>
	/// The resource endpoint, e.g. "https://my-resource.openai.azure.com". When
	/// empty, the deprecated <c>AZURE_OPENAI_ENDPOINT_HOST</c> variable is used.
	/// </summary>
	public string Endpoint { get; set; } = string.Empty;

	/// <summary>
	/// The deployment name. When empty, the deprecated
	/// <c>AZURE_OPENAI_DEPLOYMENT</c> variable is used.
	/// </summary>
	public string Deployment { get; set; } = string.Empty;

	/// <summary>
	/// <see cref="Endpoint" />, or the deprecated environment variable when it
	/// is empty.
	/// </summary>
	internal AzureSetting ResolveEndpoint(
		Func<string, string?> getEnvironmentVariable) =>
		AzureSetting.Resolve(
			Endpoint,
			ChatProvider.AZURE_ENDPOINT_ENV_VAR,
			getEnvironmentVariable);

	/// <summary>
	/// <see cref="Deployment" />, or the deprecated environment variable when
	/// it is empty.
	/// </summary>
	internal AzureSetting ResolveDeployment(
		Func<string, string?> getEnvironmentVariable) =>
		AzureSetting.Resolve(
			Deployment,
			ChatProvider.AZURE_DEPLOYMENT_ENV_VAR,
			getEnvironmentVariable);

	/// <summary>
	/// Generation budget. A reasoning deployment spends its hidden thinking from
	/// this same budget, so raise it (e.g. to 2048) if answers come back empty.
	/// </summary>
	public int MaxTokens { get; set; } = ChatProvider.DEFAULT_MAX_TOKENS;

	/// <summary>
	/// Set for a reasoning deployment (o-series, gpt-5 and later reasoning
	/// models): <c>temperature</c> is then not sent, because those models
	/// reject sampling parameters.
	/// </summary>
	public bool ReasoningModel { get; set; }

	/// <summary>
	/// How requests authenticate: <see cref="API_KEY_AUTH" /> (the default)
	/// sends <c>AZURE_OPENAI_API_KEY</c>; <see cref="AZURE_CLI_AUTH" /> sends a
	/// Microsoft Entra ID token from <c>az account get-access-token</c>, for
	/// resources that use RBAC instead of keys; <see cref="AZURE_IDENTITY_AUTH" />
	/// gets the token through Azure.Identity, with the sign-in named by
	/// <see cref="Credential" />.
	/// </summary>
	public string Auth { get; set; } = API_KEY_AUTH;

	/// <summary>
	/// The Azure.Identity sign-in for <see cref="AZURE_IDENTITY_AUTH" />: one
	/// of the <see cref="Credentials" /> names. Empty means a browser sign-in.
	/// </summary>
	public string Credential { get; set; } = INTERACTIVE_BROWSER_CREDENTIAL;

	/// <summary>
	/// The Entra ID tenant to get tokens from, as a GUID or domain. Empty means
	/// az's current tenant (<see cref="AZURE_CLI_AUTH" />) or the credential's
	/// own default (<see cref="AZURE_IDENTITY_AUTH" />).
	/// </summary>
	public string TenantId { get; set; } = string.Empty;

	/// <summary>
	/// The token scope for the Entra ID methods; empty means
	/// <see cref="AzureOpenAIScopes.DEFAULT" />.
	/// </summary>
	public string TokenScope { get; set; } = string.Empty;

	internal const string API_KEY_AUTH = "apiKey";
	internal const string AZURE_CLI_AUTH = "azureCli";
	internal const string AZURE_IDENTITY_AUTH = "azureIdentity";
	internal const string INTERACTIVE_BROWSER_CREDENTIAL = "interactiveBrowser";

	/// <summary>
	/// The <see cref="Credential" /> names, in the order the wizard offers
	/// them.
	/// </summary>
	internal static readonly (string Name, AzureCredentialKind Kind)[]
		Credentials =
		[
			(INTERACTIVE_BROWSER_CREDENTIAL,
				AzureCredentialKind.InteractiveBrowser),
			("deviceCode", AzureCredentialKind.DeviceCode),
			("default", AzureCredentialKind.Default),
			("azureCli", AzureCredentialKind.AzureCli),
			("azd", AzureCredentialKind.AzureDeveloperCli),
		];

	/// <summary>
	/// The <see cref="Auth" /> names, in the order the wizard offers them.
	/// </summary>
	internal static readonly (string Name, AzureAuthKind Kind)[] AuthMethods =
	[
		(API_KEY_AUTH, AzureAuthKind.ApiKey),
		(AZURE_CLI_AUTH, AzureAuthKind.AzureCli),
		(AZURE_IDENTITY_AUTH, AzureAuthKind.AzureIdentity),
	];

	/// <summary>
	/// The method <see cref="Auth" /> names, ignoring case; null when it names
	/// none.
	/// </summary>
	internal AzureAuthKind? AuthKind => Find(AuthMethods, Auth);

	/// <summary>
	/// The credential <see cref="Credential" /> names, or null when it names
	/// none.
	/// </summary>
	internal AzureCredentialKind? CredentialKind =>
		string.IsNullOrWhiteSpace(Credential)
			? AzureCredentialKind.InteractiveBrowser
			: Find(Credentials, Credential.Trim());

	private static TKind? Find<TKind>(
		(string Name, TKind Kind)[] entries,
		string name)
		where TKind : struct =>
		Array.FindIndex(
				entries,
				entry => entry.Name.Equals(
					name,
					StringComparison.OrdinalIgnoreCase))
			is var index and >= 0
			? entries[index].Kind
			: null;

	/// <summary>
	/// Whether this config signs in through a browser or device code (and so
	/// keeps a sign-in next to the config file).
	/// </summary>
	internal bool UsesInteractiveSignIn =>
		AuthKind == AzureAuthKind.AzureIdentity
		&& CredentialKind is { } kind
		&& AzureCredentialFactory.IsInteractive(kind);

	/// <summary>
	/// The Azure.Identity settings for <see cref="AZURE_IDENTITY_AUTH" />, or
	/// null when <see cref="Credential" /> names no credential. shelly runs in
	/// a terminal, so a browser or device code prompt is allowed.
	/// </summary>
	internal AzureCredentialOptions? CredentialOptions(
		string authenticationRecordPath) =>
		CredentialKind is { } kind
			? new AzureCredentialOptions
			{
				Kind = kind,
				TenantId = TokenProviderOptions().TenantId,
				CacheName = "shelly",
				AuthenticationRecordPath = authenticationRecordPath,
			}
			: null;

	/// <summary>
	/// The Azure CLI token options for <see cref="TenantId" /> and
	/// <see cref="TokenScope" />, with empty values left at their defaults.
	/// </summary>
	internal AzureCliTokenProviderOptions TokenProviderOptions() =>
		new()
		{
			Scope = string.IsNullOrWhiteSpace(TokenScope)
				? AzureOpenAIScopes.DEFAULT
				: TokenScope.Trim(),
			TenantId = string.IsNullOrWhiteSpace(TenantId)
				? null
				: TenantId.Trim(),
		};

	/// <summary>
	/// The config key of the token setting the Azure CLI provider would
	/// reject: <c>azure.tokenScope</c> or <c>azure.tenantId</c>; null when
	/// both are valid. Azure.Identity takes the same tenant and scope forms,
	/// so <see cref="AZURE_IDENTITY_AUTH" /> is checked with it too.
	/// </summary>
	internal string? InvalidTokenSetting() =>
		TokenProviderOptions().FindInvalidProperty() switch
		{
			nameof(AzureCliTokenProviderOptions.Scope) => "azure.tokenScope",
			nameof(AzureCliTokenProviderOptions.TenantId) => "azure.tenantId",

			// Timeout isn't a shelly setting, so it is always the default.
			_ => null,
		};
}

/// <summary>
/// Settings for any server speaking the OpenAI <c>/v1/chat/completions</c> API —
/// openai.com, or a self-hosted runner such as llama.cpp, llama-swap, Ollama, or
/// vLLM. Unlike the hosted providers, both the address and the model name are
/// deployment-specific, so there is no useful default for either.
/// </summary>
internal sealed class OpenAIConfig
{
	/// <summary>
	/// The API root including the version segment, e.g.
	/// "http://10.10.0.20:8080/v1".
	/// </summary>
	public string BaseUrl { get; set; } = string.Empty;

	/// <summary>The model name as the server reports it at <c>/v1/models</c>.</summary>
	public string Model { get; set; } = string.Empty;

	/// <summary>
	/// HTTP timeout. Defaults high because a self-hosted runner may have to load
	/// the weights from disk before it generates a single token, and a large
	/// quantized model can take minutes to answer.
	/// </summary>
	public int TimeoutSeconds { get; set; } = 300;

	/// <summary>
	/// Generation budget. Defaults well above the hosted providers' 500 because
	/// reasoning models spend most of their output on a thinking block that is
	/// discarded before the answer is read — too small a budget truncates the
	/// thought and leaves no answer at all.
	/// </summary>
	public int MaxTokens { get; set; } = 2048;
}

/// <summary>
/// An Azure setting's effective value and where it came from.
/// </summary>
/// <param name="Value">The value, or null when neither source has one.</param>
/// <param name="DeprecatedVariable">
/// The environment variable the value came from, or null when it came from
/// the config file (or is missing).
/// </param>
internal readonly record struct AzureSetting(
	string? Value,
	string? DeprecatedVariable)
{
	public bool IsSet => !string.IsNullOrWhiteSpace(Value);

	internal static AzureSetting Resolve(
		string configValue,
		string deprecatedVariable,
		Func<string, string?> getEnvironmentVariable)
	{
		if (!string.IsNullOrWhiteSpace(configValue))
		{
			return new AzureSetting(configValue.Trim(), null);
		}

		string? fromEnvironment = getEnvironmentVariable(deprecatedVariable);

		return string.IsNullOrWhiteSpace(fromEnvironment)
			? new AzureSetting(null, null)
			: new AzureSetting(fromEnvironment.Trim(), deprecatedVariable);
	}
}

[JsonSourceGenerationOptions(
	WriteIndented = true,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.Never,
	// A hand-edited "provider": null would otherwise load as null and crash
	// later; this makes it a parse error with a message instead.
	RespectNullableAnnotations = true)]
[JsonSerializable(typeof(ShellyConfig))]
internal partial class ShellyConfigJsonContext : JsonSerializerContext;
