using Xunit;

namespace JDMallen.Shelly.Tests;

public sealed class ShellyConfigFileTests : IDisposable
{
	private readonly string _directory =
		Directory.CreateTempSubdirectory("shelly-config-").FullName;

	private string ConfigPath => Path.Combine(_directory, "config.json");

	public void Dispose() => Directory.Delete(_directory, true);

	[Fact]
	public void Save_ThenLoad_RoundTrips()
	{
		var config = new ShellyConfig
		{
			Provider = "azure",
			Azure = new AzureConfig
			{
				Endpoint = "https://example.openai.azure.com",
				Deployment = "gpt-6-luna",
				MaxTokens = 2048,
				ReasoningModel = true,
			},
			Anthropic = { Model = "claude-x" },
			OpenAI = { BaseUrl = "http://localhost:8080/v1" },
		};

		config.Save(ConfigPath);
		ShellyConfig loaded = ShellyConfig.Load(
			ConfigPath,
			out string? parseError);

		Assert.Null(parseError);
		Assert.Equal("azure", loaded.Provider);
		Assert.Equal("https://example.openai.azure.com", loaded.Azure.Endpoint);
		Assert.Equal("gpt-6-luna", loaded.Azure.Deployment);
		Assert.Equal(2048, loaded.Azure.MaxTokens);
		Assert.True(loaded.Azure.ReasoningModel);
		Assert.Equal("claude-x", loaded.Anthropic.Model);
		Assert.Equal("http://localhost:8080/v1", loaded.OpenAI.BaseUrl);
	}

	[Fact]
	public void Save_LeavesNoTemporaryFileBehind()
	{
		new ShellyConfig().Save(ConfigPath);
		new ShellyConfig().Save(ConfigPath);

		Assert.Equal(
			["config.json"],
			Directory.GetFiles(_directory).Select(Path.GetFileName));
	}

	[Fact]
	public void Load_MissingFile_WritesDefaults()
	{
		ShellyConfig loaded = ShellyConfig.Load(
			ConfigPath,
			out string? parseError);

		Assert.Null(parseError);
		Assert.Equal(ShellyConfig.ANTHROPIC_PROVIDER, loaded.Provider);
		Assert.True(File.Exists(ConfigPath));
	}

	[Fact]
	public void Load_UnparseableFile_ReportsErrorAndLeavesFileAlone()
	{
		File.WriteAllText(ConfigPath, "{ not json");

		ShellyConfig loaded = ShellyConfig.Load(
			ConfigPath,
			out string? parseError);

		Assert.NotNull(parseError);
		Assert.Equal(ShellyConfig.ANTHROPIC_PROVIDER, loaded.Provider);
		Assert.Equal("{ not json", File.ReadAllText(ConfigPath));
	}

	[Fact]
	public void AzureSetting_ConfigValueBeatsDeprecatedVariable()
	{
		var azure = new AzureConfig { Endpoint = "https://config.example" };

		AzureSetting endpoint =
			azure.ResolveEndpoint(_ => "https://env.example");

		Assert.Equal(
			new AzureSetting("https://config.example", null),
			endpoint);
	}

	[Fact]
	public void AzureSetting_EmptyConfig_FallsBackToDeprecatedVariable()
	{
		AzureSetting deployment = new AzureConfig().ResolveDeployment(name
			=> name == ChatProvider.AZURE_DEPLOYMENT_ENV_VAR
				? "gpt-6-luna"
				: null);

		Assert.Equal(
			new AzureSetting(
				"gpt-6-luna",
				ChatProvider.AZURE_DEPLOYMENT_ENV_VAR),
			deployment);
	}

	[Fact]
	public void AzureSetting_NeitherSource_IsNotSet() => Assert.False(
		new AzureConfig().ResolveEndpoint(_ => null).IsSet);
}
