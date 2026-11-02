using Xunit;

namespace JDMallen.Shelly.Tests;

public class InfoReportTests
{
	private const string SECRET = "sk-super-secret-value";

	private readonly Dictionary<string, string> _environment = new();

	private string Build(ShellyConfig config) =>
		InfoReport.Build(
			"shelly 9.9.9",
			"/home/me/.config/shelly/config.json",
			config,
			Shell.FromNameOrPath("/usr/bin/zsh"),
			name => _environment.GetValueOrDefault(name));

	[Fact]
	public void Azure_ReportsEndpointDeploymentAndKeyPresence()
	{
		_environment[ChatProvider.AzureEndpointEnvVar] = "https://example.openai.azure.com";
		_environment[ChatProvider.AzureDeploymentEnvVar] = "gpt-6-luna";
		_environment[ChatProvider.AzureApiKeyEnvVar] = SECRET;

		string report = Build(
			new ShellyConfig { Provider = "azure", Azure = new AzureConfig { MaxTokens = 2048 } });

		Assert.StartsWith("shelly 9.9.9", report);
		Assert.Contains("provider:   azure", report);
		Assert.Contains(
			"endpoint:   https://example.openai.azure.com (from AZURE_OPENAI_ENDPOINT_HOST)",
			report);
		Assert.Contains("deployment: gpt-6-luna (from AZURE_OPENAI_DEPLOYMENT)", report);
		Assert.Contains("max tokens: 2048", report);
		Assert.Contains("api key:    AZURE_OPENAI_API_KEY is set", report);
		Assert.Contains("shell:      Zsh (/usr/bin/zsh -c)", report);
		Assert.DoesNotContain(SECRET, report);
	}

	[Fact]
	public void Azure_MissingVariables_SaySo()
	{
		string report = Build(new ShellyConfig { Provider = "azure" });

		Assert.Contains("(not set: AZURE_OPENAI_ENDPOINT_HOST)", report);
		Assert.Contains("(not set: AZURE_OPENAI_DEPLOYMENT)", report);
		Assert.Contains("AZURE_OPENAI_API_KEY is not set", report);
	}

	[Fact]
	public void Anthropic_ReportsModelAndFixedEndpoint()
	{
		_environment[ChatProvider.AnthropicApiKeyEnvVar] = SECRET;

		string report = Build(new ShellyConfig());

		Assert.Contains("provider:   anthropic", report);
		Assert.Contains($"endpoint:   {InfoReport.ANTHROPIC_ENDPOINT}", report);
		Assert.Contains("model:      claude-haiku-4-5-20251001", report);
		Assert.Contains("ANTHROPIC_API_KEY_SHELLY is set", report);
		Assert.DoesNotContain(SECRET, report);
	}

	[Fact]
	public void OpenAI_ReportsConfigFileSettings()
	{
		var config = new ShellyConfig
		{
			Provider = "local",
			OpenAI = new OpenAIConfig
			{
				BaseUrl = "http://10.10.0.20:8080/v1",
				Model = "qwen",
				MaxTokens = 4096,
				TimeoutSeconds = 120,
			},
		};

		string report = Build(config);

		Assert.Contains("endpoint:   http://10.10.0.20:8080/v1", report);
		Assert.Contains("model:      qwen", report);
		Assert.Contains("max tokens: 4096", report);
		Assert.Contains("timeout:    120s", report);
		Assert.Contains("OPENAI_API_KEY_SHELLY is not set (optional)", report);
	}

	[Fact]
	public void UnknownProvider_SaysSo()
	{
		string report = Build(new ShellyConfig { Provider = "gemini" });

		Assert.Contains("provider: gemini", report);
		Assert.Contains("error:    unknown provider", report);
	}
}
