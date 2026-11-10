using Xunit;

namespace JDMallen.Shelly.Tests;

public class ConfigCheckTests
{
	private readonly Dictionary<string, string> _environment = [];

	private IReadOnlyList<string> FindMissing(
		ShellyConfig config,
		string? parseError = null) =>
		ConfigCheck.FindMissing(
			config,
			parseError,
			name => _environment.GetValueOrDefault(name));

	[Fact]
	public void Anthropic_WithoutKey_NeedsKey() => Assert.Equal(
		[ChatProvider.ANTHROPIC_API_KEY_ENV_VAR],
		FindMissing(new ShellyConfig()));

	[Fact]
	public void Anthropic_WithKey_IsReady()
	{
		_environment[ChatProvider.ANTHROPIC_API_KEY_ENV_VAR] = "x";

		Assert.Empty(FindMissing(new ShellyConfig()));
	}

	[Fact]
	public void ParseError_ListsConfigFileFirst()
	{
		_environment[ChatProvider.ANTHROPIC_API_KEY_ENV_VAR] = "x";

		Assert.Equal(
			[ConfigCheck.CONFIG_FILE],
			FindMissing(new ShellyConfig(), "bad json"));
	}

	[Fact]
	public void UnknownProvider_NeedsProvider() => Assert.Equal(
		[ConfigCheck.PROVIDER_KEY],
		FindMissing(new ShellyConfig { Provider = "gemini" }));

	[Theory]
	[InlineData("openai")]
	[InlineData("local")]
	[InlineData("LOCAL")]
	public void OpenAI_KeyIsOptional(string provider)
	{
		var config = new ShellyConfig
		{
			Provider = provider,
			OpenAI = new OpenAIConfig
			{
				BaseUrl = "http://localhost:8080/v1",
				Model = "qwen",
			},
		};

		Assert.Empty(FindMissing(config));
	}

	[Fact]
	public void OpenAI_ListsBlankAndNonPositiveSettings()
	{
		var config = new ShellyConfig
		{
			Provider = "openai",
			OpenAI = new OpenAIConfig
			{
				BaseUrl = " ",
				Model = "",
				TimeoutSeconds = 0,
				MaxTokens = -1,
			},
		};

		Assert.Equal(
			[
				"openai.baseUrl", "openai.model", "openai.timeoutSeconds",
				"openai.maxTokens",
			],
			FindMissing(config));
	}

	[Fact]
	public void Azure_Unconfigured_ListsEverything() => Assert.Equal(
		[
			"azure.endpoint", "azure.deployment",
			ChatProvider.AZURE_API_KEY_ENV_VAR,
		],
		FindMissing(new ShellyConfig { Provider = "azure" }));

	[Fact]
	public void Azure_FromDeprecatedVariables_StillNeedsMoving()
	{
		_environment[ChatProvider.AZURE_ENDPOINT_ENV_VAR] =
			"https://example.openai.azure.com";
		_environment[ChatProvider.AZURE_DEPLOYMENT_ENV_VAR] = "gpt-6-luna";
		_environment[ChatProvider.AZURE_API_KEY_ENV_VAR] = "x";

		Assert.Equal(
			["azure.endpoint", "azure.deployment"],
			FindMissing(new ShellyConfig { Provider = "azure" }));
	}

	[Fact]
	public void Azure_FullyConfigured_IsReady()
	{
		_environment[ChatProvider.AZURE_API_KEY_ENV_VAR] = "x";
		var config = new ShellyConfig
		{
			Provider = "azure",
			Azure = new AzureConfig
			{
				Endpoint = "https://example.openai.azure.com",
				Deployment = "gpt-6-luna",
			},
		};

		Assert.Empty(FindMissing(config));
	}

	private static ShellyConfig AzureCliConfig(
		string tenantId = "",
		string tokenScope = "",
		string auth = AzureConfig.AZURE_CLI_AUTH) =>
		new()
		{
			Provider = "azure",
			Azure = new AzureConfig
			{
				Endpoint = "https://example.openai.azure.com",
				Deployment = "gpt-6-luna",
				Auth = auth,
				TenantId = tenantId,
				TokenScope = tokenScope,
			},
		};

	[Fact]
	public void Azure_AzureCli_NeedsAzNotKey() => Assert.Equal(
		[ConfigCheck.AZURE_CLI],
		FindMissing(AzureCliConfig()));

	[Theory]
	[InlineData(AzureConfig.AZURE_CLI_AUTH)]
	[InlineData("AZURECLI")]
	public void Azure_AzureCli_WithAzOnPath_IsReady(string auth)
	{
		DirectoryInfo binDirectory =
			Directory.CreateTempSubdirectory("shelly-az-");
		try
		{
			File.WriteAllText(
				Path.Combine(binDirectory.FullName, "az"),
				string.Empty);
			File.WriteAllText(
				Path.Combine(binDirectory.FullName, "az.cmd"),
				string.Empty);
			_environment["PATH"] = binDirectory.FullName;

			Assert.Empty(FindMissing(AzureCliConfig(auth: auth)));
		}
		finally
		{
			binDirectory.Delete(true);
		}
	}

	[Theory]
	[InlineData("not a tenant!", "", "azure.tenantId")]
	[InlineData("", "http://insecure.example/.default", "azure.tokenScope")]
	[InlineData("contoso.onmicrosoft.com", "not a url", "azure.tokenScope")]
	public void Azure_AzureCli_InvalidSetting_IsListed(
		string tenantId,
		string tokenScope,
		string expectedKey) => Assert.Equal(
		[expectedKey, ConfigCheck.AZURE_CLI],
		FindMissing(AzureCliConfig(tenantId, tokenScope)));

	[Fact]
	public void Azure_UnknownAuth_NeedsAuth() => Assert.Equal(
		["azure.auth"],
		FindMissing(AzureCliConfig(auth: "managedIdentity")));

	[Fact]
	public void Azure_AzureIdentity_NeedsNoKeyAndNoAz() => Assert.Empty(
		FindMissing(AzureCliConfig(auth: AzureConfig.AZURE_IDENTITY_AUTH)));

	[Fact]
	public void Azure_AzureIdentity_UnknownCredential_IsListed()
	{
		ShellyConfig config =
			AzureCliConfig(auth: AzureConfig.AZURE_IDENTITY_AUTH);
		config.Azure.Credential = "broker";

		Assert.Equal(["azure.credential"], FindMissing(config));
	}

	[Fact]
	public void Azure_AzureIdentity_InvalidTenant_IsListed() => Assert.Equal(
		["azure.tenantId"],
		FindMissing(
			AzureCliConfig(
				"not a tenant!",
				auth: AzureConfig.AZURE_IDENTITY_AUTH)));
}
