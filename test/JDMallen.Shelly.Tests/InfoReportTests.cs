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
		_environment[ChatProvider.AZURE_API_KEY_ENV_VAR] = SECRET;

		string report = Build(
			new ShellyConfig
			{
				Provider = "azure",
				Azure = new AzureConfig
				{
					Endpoint = "https://example.openai.azure.com",
					Deployment = "gpt-6-luna",
					MaxTokens = 2048,
					ReasoningModel = true,
				},
			});

		Assert.StartsWith("shelly 9.9.9", report);
		Assert.Contains("provider:   azure", report);
		Assert.Contains(
			"endpoint:   https://example.openai.azure.com\n",
			report.ReplaceLineEndings("\n"));
		Assert.Contains(
			"deployment: gpt-6-luna\n",
			report.ReplaceLineEndings("\n"));
		Assert.Contains("reasoning:  yes (temperature not sent)", report);
		Assert.Contains("max tokens: 2048", report);
		Assert.Contains("api key:    AZURE_OPENAI_API_KEY is set", report);
		Assert.Contains("shell:      Zsh (/usr/bin/zsh -c)", report);
		Assert.DoesNotContain(SECRET, report);
	}

	[Fact]
	public void Azure_DeprecatedVariables_ReportedAsDeprecated()
	{
		_environment[ChatProvider.AZURE_ENDPOINT_ENV_VAR] =
			"https://example.openai.azure.com";
		_environment[ChatProvider.AZURE_DEPLOYMENT_ENV_VAR] = "gpt-6-luna";

		string report = Build(new ShellyConfig { Provider = "azure" });

		Assert.Contains(
			"endpoint:   https://example.openai.azure.com (deprecated: from AZURE_OPENAI_ENDPOINT_HOST)",
			report);
		Assert.Contains(
			"deployment: gpt-6-luna (deprecated: from AZURE_OPENAI_DEPLOYMENT)",
			report);
	}

	[Fact]
	public void Azure_MissingSettings_NameTheConfigKeys()
	{
		string report = Build(new ShellyConfig { Provider = "azure" });

		Assert.Contains("(not set: azure.endpoint)", report);
		Assert.Contains("(not set: azure.deployment)", report);
		Assert.Contains("AZURE_OPENAI_API_KEY is not set", report);
	}

	[Fact]
	public void Anthropic_ReportsModelAndFixedEndpoint()
	{
		_environment[ChatProvider.ANTHROPIC_API_KEY_ENV_VAR] = SECRET;

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

	private const string USAGE_PATH = "/home/me/.local/share/shelly/usage.json";

	private string BuildWithUsage(
		UsageTotals? totals,
		string? error = null,
		bool showTokenUsage = true) =>
		InfoReport.Build(
			"shelly 9.9.9",
			"/home/me/.config/shelly/config.json",
			new ShellyConfig { ShowTokenUsage = showTokenUsage },
			Shell.FromNameOrPath("/usr/bin/zsh"),
			name => _environment.GetValueOrDefault(name),
			new InfoReport.UsageReport(USAGE_PATH, totals, error));

	[Fact]
	public void Usage_ReportsTotalsAndPerRunAverage()
	{
		string report = BuildWithUsage(
			new UsageTotals
			{
				Runs = 4,
				Requests = 6,
				InputTokens = 10_000,
				OutputTokens = 2_345,
				Since = new DateTimeOffset(2026, 11, 6, 9, 0, 0, TimeSpan.Zero),
			});

		Assert.Contains($"usage log: {USAGE_PATH}", report);
		Assert.Contains(
			"total:     12,345 tokens (10,000 in, 2,345 out) over 4 runs since 2026-11-06",
			report);
		Assert.Contains(
			"average:   3,086 tokens per run (2,500 in, 586 out)",
			report);

		// The usage block is aligned on its own, so the provider block keeps
		// its padding.
		Assert.Contains("provider:   anthropic", report);
	}

	[Fact]
	public void Usage_ReportsWhetherTheFooterIsShown()
	{
		Assert.Contains(
			"footer:    shown after the menu (showTokenUsage)",
			BuildWithUsage(new UsageTotals()));
		Assert.Contains(
			"footer:    hidden (showTokenUsage: false)",
			BuildWithUsage(new UsageTotals(), showTokenUsage: false));
	}

	[Fact]
	public void Usage_NothingRecorded_SaysSo()
	{
		string report = BuildWithUsage(new UsageTotals());

		Assert.Contains("total:     none recorded yet", report);
		Assert.DoesNotContain("average:", report);
	}

	[Fact]
	public void Usage_UnreadableFile_ShowsTheError()
	{
		string report = BuildWithUsage(null, "bad json");

		Assert.Contains("total:     (unreadable: bad json)", report);
	}
}
