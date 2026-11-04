using System.Text.Json;
using System.Text.Json.Serialization;

namespace JDMallen.Shelly;

public sealed class ShellyConfig
{
	internal const string ANTHROPIC_PROVIDER = "anthropic";
	internal const string AZURE_PROVIDER = "azure";
	internal const string OPENAI_PROVIDER = "openai";

	/// <summary>Accepted in place of <see cref="OPENAI_PROVIDER" />.</summary>
	internal const string LOCAL_PROVIDER_ALIAS = "local";

	public string Provider { get; set; } = ANTHROPIC_PROVIDER;

	public AnthropicConfig Anthropic { get; set; } = new();

	public AzureConfig Azure { get; set; } = new();

	[JsonPropertyName("openai")]
	public OpenAIConfig OpenAI { get; set; } = new();

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
		catch (JsonException ex)
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
	/// Whether <paramref name="provider" /> names the OpenAI-compatible
	/// provider, by its name or its alias.
	/// </summary>
	internal static bool IsOpenAIProvider(string provider) =>
		provider.Equals(OPENAI_PROVIDER, StringComparison.OrdinalIgnoreCase)
		|| provider.Equals(
			LOCAL_PROVIDER_ALIAS,
			StringComparison.OrdinalIgnoreCase);

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

public sealed class AnthropicConfig
{
	public string Model { get; set; } = "claude-haiku-4-5-20251001";
}

/// <summary>
/// Azure OpenAI settings that aren't secrets. Only the API key stays in an
/// environment variable.
/// </summary>
public sealed class AzureConfig
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
}

/// <summary>
/// Settings for any server speaking the OpenAI <c>/v1/chat/completions</c> API —
/// openai.com, or a self-hosted runner such as llama.cpp, llama-swap, Ollama, or
/// vLLM. Unlike the hosted providers, both the address and the model name are
/// deployment-specific, so there is no useful default for either.
/// </summary>
public sealed class OpenAIConfig
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
	DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(ShellyConfig))]
internal partial class ShellyConfigJsonContext : JsonSerializerContext;
