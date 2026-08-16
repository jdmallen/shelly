using System.Text.Json;
using System.Text.Json.Serialization;

namespace JDMallen.Shelly;

public sealed class ShellyConfig
{
	public string Provider { get; set; } = "anthropic";

	public AnthropicConfig Anthropic { get; set; } = new();

	[JsonPropertyName("openai")]
	public OpenAIConfig OpenAI { get; set; } = new();

	public static ShellyConfig Load()
	{
		string path = ConfigPath();
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
			Console.Error.WriteLine($"Warning: failed to parse {path}: {ex.Message}. Using defaults.");

			return new ShellyConfig();
		}
	}

	public static string ConfigPath()
	{
		string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		if (!string.IsNullOrEmpty(baseDir))
		{
			return Path.Combine(baseDir, "shelly", "config.json");
		}

		string? home = Environment.GetEnvironmentVariable("HOME");
		baseDir = string.IsNullOrEmpty(home) ? "." : Path.Combine(home, ".config");

		return Path.Combine(baseDir, "shelly", "config.json");
	}

	private static void TryWriteDefaults(string path, ShellyConfig defaults)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			using FileStream stream = File.Create(path);
			JsonSerializer.Serialize(stream, defaults, ShellyConfigJsonContext.Default.ShellyConfig);
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

[JsonSourceGenerationOptions(
	WriteIndented = true,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(ShellyConfig))]
internal partial class ShellyConfigJsonContext : JsonSerializerContext;
