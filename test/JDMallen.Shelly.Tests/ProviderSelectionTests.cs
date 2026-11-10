using System.Text.Json;

using JDMallen.Toolbox.AI;
using JDMallen.Toolbox.AI.Azure;

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
	private static (IChatProvider? Provider, string Error) Create(
		ShellyConfig config)
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
		(IChatProvider? provider, string error) =
			Create(new ShellyConfig { Provider = "llama" });

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
	public void CreateProvider_OpenAINonPositiveTimeout_Fails(
		int timeoutSeconds)
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

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void CreateProvider_AzureNonPositiveMaxTokens_Fails(int maxTokens)
	{
		var config = new ShellyConfig
		{
			Provider = "azure",
			Azure = { MaxTokens = maxTokens },
		};

		(IChatProvider? provider, string error) = Create(config);

		Assert.Null(provider);
		Assert.Contains("azure.maxTokens", error);
	}

	private static ShellyConfig AzureConfigWithAuth(
		string auth,
		string tenantId = "") =>
		new()
		{
			Provider = "azure",
			Azure = new AzureConfig
			{
				Endpoint = "https://example.openai.azure.com",
				Deployment = "gpt-6-luna",
				Auth = auth,
				TenantId = tenantId,
			},
		};

	// No az process starts here: the token is fetched on the first request.
	[Fact]
	public void CreateProvider_AzureCli_SucceedsWithoutApiKey()
	{
		(IChatProvider? provider, string error) = Create(
			AzureConfigWithAuth(AzureConfig.AZURE_CLI_AUTH));

		Assert.NotNull(provider);
		Assert.Empty(error);
	}

	[Fact]
	public void CreateProvider_AzureCliInvalidTenant_FailsNamingTheSetting()
	{
		(IChatProvider? provider, string error) = Create(
			AzureConfigWithAuth(AzureConfig.AZURE_CLI_AUTH, "not a tenant!"));

		Assert.Null(provider);
		Assert.Contains("azure.tenantId", error);
	}

	// Nothing signs in here either: the first request does.
	[Fact]
	public void CreateProvider_AzureIdentity_SucceedsWithoutApiKey()
	{
		(IChatProvider? provider, string error) = Create(
			AzureConfigWithAuth(AzureConfig.AZURE_IDENTITY_AUTH));

		Assert.NotNull(provider);
		Assert.Empty(error);
	}

	[Fact]
	public void CreateProvider_AzureIdentityUnknownCredential_FailsNamingTheSetting()
	{
		ShellyConfig config =
			AzureConfigWithAuth(AzureConfig.AZURE_IDENTITY_AUTH);
		config.Azure.Credential = "broker";

		(IChatProvider? provider, string error) = Create(config);

		Assert.Null(provider);
		Assert.Contains("azure.credential", error);
	}

	[Fact]
	public void CreateProvider_AzureIdentityInvalidTenant_FailsNamingTheSetting()
	{
		(IChatProvider? provider, string error) = Create(
			AzureConfigWithAuth(
				AzureConfig.AZURE_IDENTITY_AUTH,
				"not a tenant!"));

		Assert.Null(provider);
		Assert.Contains("azure.tenantId", error);
	}

	[Fact]
	public void CreateProvider_AzureUnknownAuth_FailsListingTheOptions()
	{
		(IChatProvider? provider, string error) = Create(
			AzureConfigWithAuth("managedIdentity"));

		Assert.Null(provider);
		Assert.Contains("'managedIdentity'", error);
		Assert.Contains("'apiKey', 'azureCli' or 'azureIdentity'", error);
	}
}

public class ShellyConfigTests
{
	private static ShellyConfig Deserialize(string json) =>
		JsonSerializer.Deserialize(
			json,
			ShellyConfigJsonContext.Default.ShellyConfig)!;

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
	public void Deserialize_ReadsAzureMaxTokens_DefaultsTo500()
	{
		AzureConfig defaults = Deserialize("{ \"provider\": \"azure\" }").Azure;
		Assert.Equal(500, defaults.MaxTokens);
		Assert.False(defaults.ReasoningModel);
		Assert.True(
			Deserialize("{ \"azure\": { \"reasoningModel\": true } }")
				.Azure.ReasoningModel);
		Assert.Equal(
			2048,
			Deserialize("{ \"azure\": { \"maxTokens\": 2048 } }")
				.Azure.MaxTokens);
	}

	[Fact]
	public void Deserialize_ShowTokenUsage_DefaultsToTrue()
	{
		Assert.True(
			Deserialize("""{ "provider": "anthropic" }""").ShowTokenUsage);
		Assert.False(
			Deserialize("""{ "showTokenUsage": false }""").ShowTokenUsage);
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

	[Fact]
	public void Deserialize_AzureAuth_DefaultsToApiKey()
	{
		AzureConfig defaults = Deserialize("{ \"provider\": \"azure\" }").Azure;
		Assert.Equal(AzureConfig.API_KEY_AUTH, defaults.Auth);
		Assert.True(defaults.UsesApiKey);
		Assert.Equal(string.Empty, defaults.TenantId);
		Assert.Equal(string.Empty, defaults.TokenScope);

		AzureConfig azureCli = Deserialize(
				"{ \"azure\": { \"auth\": \"azureCli\", \"tenantId\": \"contoso.onmicrosoft.com\", \"tokenScope\": \"https://cognitiveservices.azure.com/.default\" } }")
			.Azure;
		Assert.True(azureCli.UsesAzureCli);
		Assert.Equal(
			new AzureCliTokenProviderOptions
			{
				TenantId = "contoso.onmicrosoft.com",
				Scope = AzureOpenAIScopes.COGNITIVE_SERVICES,
			},
			azureCli.TokenProviderOptions());
	}

	[Fact]
	public void Serialize_WritesAzureAuthSettingsAsATemplate()
	{
		string json = JsonSerializer.Serialize(
			new ShellyConfig(),
			ShellyConfigJsonContext.Default.ShellyConfig);

		Assert.Contains("\"auth\": \"apiKey\"", json);
		Assert.Contains("\"tenantId\"", json);
		Assert.Contains("\"tokenScope\"", json);
	}

	[Fact]
	public void AzureIdentity_Deserializes_WithCredentialAndRecordNextToConfig()
	{
		AzureConfig azure = Deserialize(
				"{ \"azure\": { \"auth\": \"azureIdentity\", \"credential\": \"DeviceCode\", \"tenantId\": \" contoso.com \" } }")
			.Azure;

		Assert.True(azure.UsesAzureIdentity);
		Assert.Equal(AzureCredentialKind.DeviceCode, azure.CredentialKind);
		Assert.True(azure.UsesInteractiveSignIn);

		string recordPath = ShellyConfig.AuthenticationRecordPath(
			Path.Combine("/home/me/.config/shelly", "config.json"));
		AzureCredentialOptions options = azure.CredentialOptions(recordPath)!;
		Assert.Equal(AzureCredentialKind.DeviceCode, options.Kind);
		Assert.Equal("contoso.com", options.TenantId);
		Assert.Equal("shelly", options.CacheName);
		Assert.Equal(
			Path.Combine(
				Path.GetFullPath("/home/me/.config/shelly"),
				"auth-record.json"),
			options.AuthenticationRecordPath);
		Assert.True(options.AllowInteractiveAuthentication);
	}

	[Fact]
	public void AzureIdentity_MissingCredential_IsABrowserSignIn()
	{
		AzureConfig azure = Deserialize(
				"{ \"azure\": { \"auth\": \"azureIdentity\" } }")
			.Azure;

		Assert.Equal(
			AzureCredentialKind.InteractiveBrowser,
			azure.CredentialKind);
	}

	[Theory]
	[InlineData("default", false)]
	[InlineData("azureCli", false)]
	[InlineData("azd", false)]
	[InlineData("interactiveBrowser", true)]
	public void AzureIdentity_OnlyBrowserAndDeviceCodeAreInteractive(
		string credential,
		bool interactive) => Assert.Equal(
		interactive,
		new AzureConfig
		{
			Auth = AzureConfig.AZURE_IDENTITY_AUTH,
			Credential = credential,
		}.UsesInteractiveSignIn);

	[Fact]
	public void AzureIdentity_UnknownCredential_HasNoOptions()
	{
		var azure = new AzureConfig
		{
			Auth = AzureConfig.AZURE_IDENTITY_AUTH,
			Credential = "broker",
		};

		Assert.Null(azure.CredentialKind);
		Assert.Null(azure.CredentialOptions("/tmp/auth-record.json"));
	}
}
