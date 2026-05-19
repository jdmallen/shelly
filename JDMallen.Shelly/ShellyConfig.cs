using System.Text.Json;
using System.Text.Json.Serialization;

namespace JDMallen.Shelly;

public sealed class ShellyConfig
{
	public string Provider { get; set; } = "anthropic";

	public AnthropicConfig Anthropic { get; set; } = new();

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
			ShellyConfig? loaded = JsonSerializer.Deserialize(stream, ShellyConfigJsonContext.Default.ShellyConfig);

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
		if (string.IsNullOrEmpty(baseDir))
		{
			string? home = Environment.GetEnvironmentVariable("HOME");
			baseDir = string.IsNullOrEmpty(home) ? "." : Path.Combine(home, ".config");
		}

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

[JsonSourceGenerationOptions(
	WriteIndented = true,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(ShellyConfig))]
internal partial class ShellyConfigJsonContext : JsonSerializerContext;
