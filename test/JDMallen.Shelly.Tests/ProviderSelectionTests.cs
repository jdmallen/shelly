using System.Text.Json;
using Xunit;

namespace JDMallen.Shelly.Tests;

public class ProviderSelectionTests
{
	private static ShellyConfig LocalConfig() =>
		new()
		{
			Provider = "openai",
			OpenAI = new OpenAIConfig
			{
				BaseUrl = "http://10.10.0.20:8080/v1",
				Model = "thinkingcap-27b-q4km",
			},
		};

	/// <summary>
	/// Runs <see cref="Program.CreateProvider" /> with stderr captured, so the
	/// failure message is assertable without leaking into the test output.
	/// </summary>
	private static (IChatProvider? Provider, string Error) Create(ShellyConfig config)
	{
		TextWriter original = Console.Error;
		var captured = new StringWriter();
		Console.SetError(captured);

		try
		{
			return (Program.CreateProvider(config), captured.ToString());
		}
		finally
		{
			Console.SetError(original);
		}
	}

	[Theory]
	[InlineData("openai")]
	[InlineData("OpenAI")]
	[InlineData("local")]
	public void CreateProvider_LocalAliases_ReturnProvider(string providerName)
	{
		ShellyConfig config = LocalConfig();
		config.Provider = providerName;

		(IChatProvider? provider, string error) = Create(config);

		Assert.NotNull(provider);
		Assert.Empty(error);
	}

	[Fact]
	public void CreateProvider_UnknownProvider_ListsOpenAIAsAnOption()
	{
		(IChatProvider? provider, string error) = Create(new ShellyConfig { Provider = "llama" });

		Assert.Null(provider);
		Assert.Contains("Unknown provider 'llama'", error);
		Assert.Contains("openai", error);
	}

	[Theory]
	[InlineData("", "thinkingcap-27b-q4km", "openai.baseUrl")]
	[InlineData("   ", "thinkingcap-27b-q4km", "openai.baseUrl")]
	[InlineData("http://10.10.0.20:8080/v1", "", "openai.model")]
	public void CreateProvider_OpenAIMissingSetting_FailsNamingTheSetting(
		string baseUrl,
		string model,
		string expectedInError)
	{
		ShellyConfig config = LocalConfig();
		config.OpenAI.BaseUrl = baseUrl;
		config.OpenAI.Model = model;

		(IChatProvider? provider, string error) = Create(config);

		Assert.Null(provider);
		Assert.Contains(expectedInError, error);
		Assert.Contains(ShellyConfig.ConfigPath(), error);
	}

	[Fact]
	public void CreateProvider_OpenAIBothSettingsMissing_NamesBoth()
	{
		var config = new ShellyConfig { Provider = "openai" };

		(IChatProvider? provider, string error) = Create(config);

		Assert.Null(provider);
		Assert.Contains("openai.baseUrl", error);
		Assert.Contains("openai.model", error);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void CreateProvider_OpenAINonPositiveTimeout_Fails(int timeoutSeconds)
	{
		ShellyConfig config = LocalConfig();
		config.OpenAI.TimeoutSeconds = timeoutSeconds;

		(IChatProvider? provider, string error) = Create(config);

		Assert.Null(provider);
		Assert.Contains("openai.timeoutSeconds", error);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void CreateProvider_OpenAINonPositiveMaxTokens_Fails(int maxTokens)
	{
		ShellyConfig config = LocalConfig();
		config.OpenAI.MaxTokens = maxTokens;

		(IChatProvider? provider, string error) = Create(config);

		Assert.Null(provider);
		Assert.Contains("openai.maxTokens", error);
	}
}

public class ShellyConfigTests
{
	private static ShellyConfig Deserialize(string json) =>
		JsonSerializer.Deserialize(json, ShellyConfigJsonContext.Default.ShellyConfig)!;

	[Fact]
	public void OpenAIDefaults_AreEmptyAddressAndGenerousBudget()
	{
		var config = new OpenAIConfig();

		Assert.Equal(string.Empty, config.BaseUrl);
		Assert.Equal(string.Empty, config.Model);

		// A cold self-hosted model can take minutes; a reasoning model spends most
		// of its budget on a thinking block that never reaches the caller.
		Assert.Equal(300, config.TimeoutSeconds);
		Assert.Equal(2048, config.MaxTokens);
	}

	[Fact]
	public void Deserialize_ReadsLowercaseOpenaiBlock()
	{
		ShellyConfig config = Deserialize(
			"""
			{
			  "provider": "openai",
			  "openai": {
			    "baseUrl": "http://10.10.0.20:8080/v1",
			    "model": "thinkingcap-27b-q4km",
			    "timeoutSeconds": 120,
			    "maxTokens": 4096
			  }
			}
			""");

		Assert.Equal("openai", config.Provider);
		Assert.Equal("http://10.10.0.20:8080/v1", config.OpenAI.BaseUrl);
		Assert.Equal("thinkingcap-27b-q4km", config.OpenAI.Model);
		Assert.Equal(120, config.OpenAI.TimeoutSeconds);
		Assert.Equal(4096, config.OpenAI.MaxTokens);
	}

	[Fact]
	public void Deserialize_OmittedOpenAIBlock_KeepsDefaults()
	{
		ShellyConfig config = Deserialize("""{ "provider": "anthropic" }""");

		Assert.NotNull(config.OpenAI);
		Assert.Equal(string.Empty, config.OpenAI.BaseUrl);
	}

	[Fact]
	public void Serialize_WritesOpenAIBlockAsATemplate()
	{
		// The defaults file is what users edit, so the block has to appear in it.
		string json = JsonSerializer.Serialize(
			new ShellyConfig(),
			ShellyConfigJsonContext.Default.ShellyConfig);

		Assert.Contains("\"openai\"", json);
		Assert.Contains("\"baseUrl\"", json);
		Assert.Contains("\"timeoutSeconds\"", json);
	}
}
