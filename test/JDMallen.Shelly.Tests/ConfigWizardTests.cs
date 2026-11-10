using JDMallen.Shelly.Tests.Fakes;

using Xunit;

namespace JDMallen.Shelly.Tests;

public sealed class ConfigWizardTests : IDisposable
{
	private const string SECRET = "sk-super-secret-value";

	private static readonly DateTime FixedUtcNow = new(
		2026,
		11,
		3,
		19,
		37,
		0,
		DateTimeKind.Utc);

	private readonly string _directory =
		Directory.CreateTempSubdirectory("shelly-wizard-").FullName;

	private readonly FakeConsoleIO _io = new();

	private string ConfigPath => Path.Combine(_directory, "config.json");

	private string Output => _io.Output.ToString();

	public void Dispose() => Directory.Delete(_directory, true);

	private ConfigWizardOutcome Run(
		FakeUserEnvironment? environment = null,
		string shell = "zsh",
		bool fromMainMenu = false) =>
		new ConfigWizard(
				_io,
				environment ?? new FakeUserEnvironment(),
				Shell.FromNameOrPath(shell),
				ConfigPath,
				() => FixedUtcNow)
			.Run(fromMainMenu);

	private ShellyConfig Saved()
	{
		ShellyConfig config = ShellyConfig.Load(
			ConfigPath,
			out string? parseError);
		Assert.Null(parseError);

		return config;
	}

	[Fact]
	public void Anthropic_EnterKeepsDefaults_SavesAndPrintsZshLine()
	{
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // provider: current (anthropic)
		_io.ReadLines.Enqueue("");              // anthropic.model: keep
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // save: yes

		Assert.Equal(ConfigWizardOutcome.Saved, Run());

		ShellyConfig saved = Saved();
		Assert.Equal(ShellyConfig.ANTHROPIC_PROVIDER, saved.Provider);
		Assert.Equal(new AnthropicConfig().Model, saved.Anthropic.Model);
		Assert.Contains("ANTHROPIC_API_KEY_SHELLY is not set.", Output);
		Assert.Contains("~/.zshrc", Output);
		Assert.Contains("export ANTHROPIC_API_KEY_SHELLY='<your-key>'", Output);
	}

	[Fact]
	public void ProviderMenu_ListsAnthropicThenOpenAIThenAzure()
	{
		_io.ReadKeys.Enqueue(ConsoleKey.Escape);

		Run();

		string output = Output.ReplaceLineEndings("\n");
		Assert.Contains("  1) Anthropic (Claude) (default)\n", output);
		Assert.Contains(
			"  2) OpenAI-compatible server (openai.com, llama.cpp, Ollama, vLLM, ...)\n",
			output);
		Assert.Contains("  3) Azure OpenAI\n", output);
	}

	[Theory]
	[InlineData(true, "  0) Back to main menu\n")]
	[InlineData(false, "  0) Exit\n")]
	public void ProviderMenu_ZeroLabelDependsOnEntryPoint(
		bool fromMainMenu,
		string expected)
	{
		_io.ReadKeys.Enqueue(ConsoleKey.D0);

		Run(fromMainMenu: fromMainMenu);

		Assert.Contains(expected, Output.ReplaceLineEndings("\n"));
	}

	[Theory]
	[InlineData(ConsoleKey.D0)]
	[InlineData(ConsoleKey.Escape)]
	public void ProviderMenu_BackingOut_ChangesNothing(ConsoleKey key)
	{
		new ShellyConfig { Provider = "openai" }.Save(ConfigPath);
		string before = File.ReadAllText(ConfigPath);
		_io.ReadKeys.Enqueue(key);

		Assert.Equal(ConfigWizardOutcome.Discarded, Run(fromMainMenu: true));

		Assert.Equal(before, File.ReadAllText(ConfigPath));
		Assert.Single(Directory.GetFiles(_directory));
		Assert.DoesNotContain(Strings.SettingsHeader, Output);
		Assert.DoesNotContain(Strings.ConfigNotSaved, Output);
		Assert.Empty(_io.ReadKeys);
	}

	[Fact]
	public void OpenAI_ValidatesInputAndSaves()
	{
		_io.ReadKeys.Enqueue(ConsoleKey.D2);
		_io.ReadLines.Enqueue("localhost:8080"); // invalid URL
		_io.ReadLines.Enqueue("http://localhost:8080/v1"); // openai.baseUrl
		_io.ReadLines.Enqueue("qwen"); // openai.model
		_io.ReadLines.Enqueue("soon"); // invalid timeout
		_io.ReadLines.Enqueue(""); // openai.timeoutSeconds: keep
		_io.ReadLines.Enqueue("4096"); // openai.maxTokens
		_io.ReadKeys.Enqueue(ConsoleKey.Y); // save

		Assert.Equal(ConfigWizardOutcome.Saved, Run());

		ShellyConfig saved = Saved();
		Assert.Equal(ShellyConfig.OPENAI_PROVIDER, saved.Provider);
		Assert.Equal("http://localhost:8080/v1", saved.OpenAI.BaseUrl);
		Assert.Equal("qwen", saved.OpenAI.Model);
		Assert.Equal(300, saved.OpenAI.TimeoutSeconds);
		Assert.Equal(4096, saved.OpenAI.MaxTokens);
		Assert.Contains(Strings.InvalidHttpUrl, Output);
		Assert.Contains(Strings.InvalidPositiveInteger, Output);
		Assert.Contains(Strings.ApiKeyOptional, Output);
	}

	[Fact]
	public void OpenAI_KeepsLocalAlias()
	{
		new ShellyConfig
		{
			Provider = "local",
			OpenAI = new OpenAIConfig
			{
				BaseUrl = "http://localhost:8080/v1",
				Model = "qwen",
			},
		}.Save(ConfigPath);
		_io.ReadKeys.Enqueue(
			ConsoleKey.Enter); // provider: current (OpenAI-compatible)
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // save

		Assert.Equal(ConfigWizardOutcome.Saved, Run());

		Assert.Equal("local", Saved().Provider);
	}

	[Fact]
	public void Azure_PrefillsFromDeprecatedVariablesAndPersistsKeyOnWindows()
	{
		var environment = new FakeUserEnvironment(true)
		{
			Variables =
			{
				[ChatProvider.AZURE_ENDPOINT_ENV_VAR] =
					"https://example.openai.azure.com",
			},
		};
		_io.ReadKeys.Enqueue(ConsoleKey.D3);
		_io.ReadLines.Enqueue(""); // azure.endpoint: keep the env value
		_io.ReadLines.Enqueue("gpt-6-luna"); // azure.deployment
		_io.ReadLines.Enqueue("y"); // azure.reasoningModel
		_io.ReadLines.Enqueue("2048"); // azure.maxTokens
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // auth: current (API key)
		_io.ReadKeys.Enqueue(
			ConsoleKey.Enter); // set the user variable: yes (default)
		_io.ReadSecrets.Enqueue(SECRET);
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // save

		Assert.Equal(ConfigWizardOutcome.Saved, Run(environment));

		ShellyConfig saved = Saved();
		Assert.Equal(ShellyConfig.AZURE_PROVIDER, saved.Provider);
		Assert.Equal("https://example.openai.azure.com", saved.Azure.Endpoint);
		Assert.Equal("gpt-6-luna", saved.Azure.Deployment);
		Assert.True(saved.Azure.ReasoningModel);
		Assert.Equal(2048, saved.Azure.MaxTokens);
		Assert.Equal(
			SECRET,
			environment.Persisted[ChatProvider.AZURE_API_KEY_ENV_VAR]);
		Assert.DoesNotContain(SECRET, Output);
		Assert.DoesNotContain(SECRET, File.ReadAllText(ConfigPath));
	}

	[Fact]
	public void Azure_ReasoningModel_ShowsCurrentValueAndEnterKeepsIt()
	{
		new ShellyConfig
		{
			Provider = "azure",
			Azure = new AzureConfig
			{
				Endpoint = "https://example.openai.azure.com",
				Deployment = "gpt-6-luna",
				ReasoningModel = true,
			},
		}.Save(ConfigPath);
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // provider: current (azure)
		_io.ReadLines.Enqueue("");              // azure.endpoint
		_io.ReadLines.Enqueue("");              // azure.deployment
		_io.ReadLines.Enqueue("maybe");         // azure.reasoningModel: invalid
		_io.ReadLines.Enqueue("");              // azure.reasoningModel: keep
		_io.ReadLines.Enqueue("");              // azure.maxTokens
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // auth: current (API key)
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // save

		Assert.Equal(ConfigWizardOutcome.Saved, Run());

		Assert.True(Saved().Azure.ReasoningModel);
		Assert.Contains("  azure.reasoningModel (y/n) [y]: ", Output);
		Assert.Contains(Strings.InvalidChoice, Output);
	}

	[Fact]
	public void Azure_AzureCli_SkipsKeyStepAndPrintsSignInCommand()
	{
		string binDirectory = Directory.CreateDirectory(
				Path.Combine(_directory, "bin"))
			.FullName;

		// Both names, so the lookup succeeds on Windows and Unix alike.
		File.WriteAllText(Path.Combine(binDirectory, "az"), string.Empty);
		File.WriteAllText(Path.Combine(binDirectory, "az.cmd"), string.Empty);
		var environment = new FakeUserEnvironment
		{
			Variables = { ["PATH"] = binDirectory },
		};
		_io.ReadKeys.Enqueue(ConsoleKey.D3); // provider: azure
		_io.ReadLines.Enqueue("https://example.openai.azure.com");
		_io.ReadLines.Enqueue("gpt-6-luna"); // azure.deployment
		_io.ReadLines.Enqueue(""); // azure.reasoningModel
		_io.ReadLines.Enqueue(""); // azure.maxTokens
		_io.ReadKeys.Enqueue(ConsoleKey.D2); // auth: Azure CLI
		_io.ReadLines.Enqueue("contoso.onmicrosoft.com"); // azure.tenantId
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // save

		Assert.Equal(ConfigWizardOutcome.Saved, Run(environment));

		AzureConfig saved = Saved().Azure;
		Assert.Equal(AzureConfig.AZURE_CLI_AUTH, saved.Auth);
		Assert.Equal("contoso.onmicrosoft.com", saved.TenantId);
		Assert.Contains(Strings.AzureCliSignInHeader, Output);
		Assert.Contains("Found az at ", Output);
		Assert.Contains(
			"az login --tenant contoso.onmicrosoft.com --scope https://ai.azure.com/.default",
			Output);
		Assert.Contains("Cognitive Services OpenAI User", Output);
		Assert.DoesNotContain(Strings.ApiKeyHeader, Output);
		Assert.DoesNotContain(
			ChatProvider.AZURE_API_KEY_ENV_VAR + " is",
			Output);
	}

	[Fact]
	public void Azure_AzureCli_InvalidTenantRepromptsAndDashClears()
	{
		new ShellyConfig
		{
			Provider = "azure",
			Azure = new AzureConfig
			{
				Endpoint = "https://example.openai.azure.com",
				Deployment = "gpt-6-luna",
				Auth = AzureConfig.AZURE_CLI_AUTH,
				TenantId = "old.example.com",
			},
		}.Save(ConfigPath);
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // provider: current (azure)
		_io.ReadLines.Enqueue("");              // azure.endpoint
		_io.ReadLines.Enqueue("");              // azure.deployment
		_io.ReadLines.Enqueue("");              // azure.reasoningModel
		_io.ReadLines.Enqueue("");              // azure.maxTokens
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // auth: current (Azure CLI)
		_io.ReadLines.Enqueue("not a tenant!"); // azure.tenantId: invalid
		_io.ReadLines.Enqueue("-");             // azure.tenantId: clear
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // save

		Assert.Equal(ConfigWizardOutcome.Saved, Run());

		Assert.Equal(string.Empty, Saved().Azure.TenantId);
		Assert.Contains(
			"  azure.tenantId (optional, - clears it) [old.example.com]: ",
			Output);
		Assert.Contains(Strings.InvalidTenantId, Output);
		Assert.Contains("az was not found on PATH.", Output);
		Assert.Contains(
			"az login --scope https://ai.azure.com/.default",
			Output);
	}

	[Fact]
	public void Azure_SwitchingBackToApiKey_ShowsKeyStep()
	{
		new ShellyConfig
		{
			Provider = "azure",
			Azure = new AzureConfig
			{
				Endpoint = "https://example.openai.azure.com",
				Deployment = "gpt-6-luna",
				Auth = AzureConfig.AZURE_CLI_AUTH,
			},
		}.Save(ConfigPath);
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // provider: current (azure)
		_io.ReadLines.Enqueue("");              // azure.endpoint
		_io.ReadLines.Enqueue("");              // azure.deployment
		_io.ReadLines.Enqueue("");              // azure.reasoningModel
		_io.ReadLines.Enqueue("");              // azure.maxTokens
		_io.ReadKeys.Enqueue(ConsoleKey.D1);    // auth: API key
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // save

		Assert.Equal(ConfigWizardOutcome.Saved, Run());

		Assert.Equal(AzureConfig.API_KEY_AUTH, Saved().Azure.Auth);
		Assert.Contains(
			Strings.ApiKeyNotSet(ChatProvider.AZURE_API_KEY_ENV_VAR),
			Output);
		Assert.DoesNotContain(Strings.AzureCliSignInHeader, Output);
	}

	[Fact]
	public void Azure_AzureIdentity_AsksForCredentialAndTenant_SkipsKeyStep()
	{
		_io.ReadKeys.Enqueue(ConsoleKey.D3); // provider: azure
		_io.ReadLines.Enqueue("https://example.openai.azure.com");
		_io.ReadLines.Enqueue("gpt-6-luna"); // azure.deployment
		_io.ReadLines.Enqueue(""); // azure.reasoningModel
		_io.ReadLines.Enqueue(""); // azure.maxTokens
		_io.ReadKeys.Enqueue(ConsoleKey.D3); // auth: Azure.Identity
		_io.ReadKeys.Enqueue(ConsoleKey.D2); // credential: deviceCode
		_io.ReadLines.Enqueue("contoso.onmicrosoft.com"); // azure.tenantId
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // save

		Assert.Equal(ConfigWizardOutcome.Saved, Run());

		AzureConfig saved = Saved().Azure;
		Assert.Equal(AzureConfig.AZURE_IDENTITY_AUTH, saved.Auth);
		Assert.Equal("deviceCode", saved.Credential);
		Assert.Equal("contoso.onmicrosoft.com", saved.TenantId);
		string output = Output.ReplaceLineEndings("\n");
		Assert.Contains(Strings.AzureCredentialHeader, output);
		Assert.Contains("  1) interactiveBrowser (default)\n", output);
		Assert.Contains(Strings.AzureIdentityTenantHint, output);
		Assert.Contains(Strings.AzureIdentitySignInHeader, output);
		Assert.Contains(Strings.AzureIdentitySignInNote, output);
		Assert.Contains("Cognitive Services OpenAI User", output);
		Assert.DoesNotContain(Strings.ApiKeyHeader, output);
		Assert.DoesNotContain(Strings.AzureCliSignInHeader, output);
	}

	[Fact]
	public void Azure_AzureIdentity_NonInteractiveCredential_HasNoTenantHint()
	{
		new ShellyConfig
		{
			Provider = "azure",
			Azure = new AzureConfig
			{
				Endpoint = "https://example.openai.azure.com",
				Deployment = "gpt-6-luna",
				Auth = AzureConfig.AZURE_IDENTITY_AUTH,
				Credential = "default",
			},
		}.Save(ConfigPath);
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // provider: current (azure)
		_io.ReadLines.Enqueue("");              // azure.endpoint
		_io.ReadLines.Enqueue("");              // azure.deployment
		_io.ReadLines.Enqueue("");              // azure.reasoningModel
		_io.ReadLines.Enqueue("");              // azure.maxTokens
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // auth: current (Azure.Identity)
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // credential: current (default)
		_io.ReadLines.Enqueue("");              // azure.tenantId
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // save

		Assert.Equal(ConfigWizardOutcome.Saved, Run());

		Assert.Equal("default", Saved().Azure.Credential);
		Assert.Contains("  3) default (default)", Output);
		Assert.DoesNotContain(Strings.AzureIdentityTenantHint, Output);
	}

	[Fact]
	public void Windows_DecliningSave_DoesNotPersistTypedKey()
	{
		var environment = new FakeUserEnvironment(true);
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // provider
		_io.ReadLines.Enqueue("");              // anthropic.model
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // set the variable: yes
		_io.ReadSecrets.Enqueue(SECRET);
		_io.ReadKeys.Enqueue(ConsoleKey.N); // save: no

		Assert.Equal(ConfigWizardOutcome.Discarded, Run(environment));

		Assert.Empty(environment.Persisted);
		Assert.DoesNotContain(
			Strings.UserVariableSaved(ChatProvider.ANTHROPIC_API_KEY_ENV_VAR),
			Output);
	}

	[Fact]
	public void Windows_KeyAlreadySet_DefaultsToKeepingIt()
	{
		var environment = new FakeUserEnvironment(true)
		{
			Variables = { [ChatProvider.ANTHROPIC_API_KEY_ENV_VAR] = SECRET },
		};
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // provider
		_io.ReadLines.Enqueue("");              // anthropic.model
		_io.ReadKeys.Enqueue(
			ConsoleKey.Enter); // replace the variable: no (default)
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // save

		Assert.Equal(ConfigWizardOutcome.Saved, Run(environment));

		Assert.Empty(environment.Persisted);
		Assert.Contains("ANTHROPIC_API_KEY_SHELLY is set.", Output);
		Assert.Contains("[y/N]", Output);
	}

	[Fact]
	public void Windows_EmptySecret_Skips()
	{
		var environment = new FakeUserEnvironment(true);
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // provider
		_io.ReadLines.Enqueue("");              // anthropic.model
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // set the variable: yes
		_io.ReadSecrets.Enqueue("");
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // save

		Run(environment);

		Assert.Empty(environment.Persisted);
		Assert.Contains(Strings.Skipped, Output);
	}

	[Fact]
	public void Unix_KeyAlreadySet_PrintsNoInstructions()
	{
		var environment = new FakeUserEnvironment
		{
			Variables =
			{
				[ChatProvider.ANTHROPIC_API_KEY_ENV_VAR] = SECRET,
			},
		};
		_io.ReadKeys.Enqueue(ConsoleKey.Enter);
		_io.ReadLines.Enqueue("");
		_io.ReadKeys.Enqueue(ConsoleKey.Enter);

		Run(environment);

		Assert.DoesNotContain("export ", Output);
		Assert.DoesNotContain(SECRET, Output);
	}

	[Fact]
	public void DecliningSave_LeavesFileUntouched()
	{
		new ShellyConfig { Provider = "openai" }.Save(ConfigPath);
		string before = File.ReadAllText(ConfigPath);
		_io.ReadKeys.Enqueue(ConsoleKey.D1); // switch to anthropic
		_io.ReadLines.Enqueue("claude-other");
		_io.ReadKeys.Enqueue(ConsoleKey.N); // save: no

		Assert.Equal(ConfigWizardOutcome.Discarded, Run());

		Assert.Equal(before, File.ReadAllText(ConfigPath));
		Assert.Contains(Strings.ConfigNotSaved, Output);
	}

	[Fact]
	public void UnparseableFile_DecliningToContinue_LeavesItAlone()
	{
		File.WriteAllText(ConfigPath, "{ not json");
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // continue: no (default)

		Assert.Equal(ConfigWizardOutcome.Discarded, Run());

		Assert.Equal("{ not json", File.ReadAllText(ConfigPath));
		Assert.Single(Directory.GetFiles(_directory));
	}

	[Fact]
	public void UnparseableFile_Continuing_BacksUpBeforeSaving()
	{
		File.WriteAllText(ConfigPath, "{ not json");
		_io.ReadKeys.Enqueue(ConsoleKey.Y);     // continue
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // provider
		_io.ReadLines.Enqueue("");              // anthropic.model
		_io.ReadKeys.Enqueue(ConsoleKey.Enter); // save

		Assert.Equal(ConfigWizardOutcome.Saved, Run());

		var backupPath = $"{ConfigPath}.bak_20261103T193700Z";
		Assert.Equal("{ not json", File.ReadAllText(backupPath));
		Assert.Equal(ShellyConfig.ANTHROPIC_PROVIDER, Saved().Provider);
		Assert.Contains(backupPath, Output);
	}

	[Theory]
	[InlineData("zsh", "export NAME='<your-key>'", "~/.zshrc")]
	[InlineData("/bin/bash", "export NAME='<your-key>'", "~/.bashrc")]
	[InlineData("dash", "export NAME='<your-key>'", "~/.profile")]
	[InlineData("fish", "set -Ux NAME '<your-key>'", null)]
	[InlineData("tcsh", "setenv NAME '<your-key>'", "~/.tcshrc")]
	[InlineData("pwsh", "$env:NAME = '<your-key>'", "$PROFILE")]
	[InlineData("nu", "$env.NAME = '<your-key>'", "$nu.env-path")]
	[InlineData(
		"elvish",
		"set E:NAME = '<your-key>'",
		"~/.config/elvish/rc.elv")]
	public void StartupLine_MatchesShell(
		string shell,
		string expectedLine,
		string? expectedFile)
	{
		(string line, string? startupFile) = ConfigWizard.StartupLine(
			Shell.FromNameOrPath(shell),
			"NAME");

		Assert.Equal(expectedLine, line);
		Assert.Equal(expectedFile, startupFile);
	}

	[Fact]
	public void StartupLine_UnknownShell_FallsBackToExport()
	{
		(string line, string? startupFile) = ConfigWizard.StartupLine(
			Shell.FromNameOrPath("xonsh"),
			"NAME");

		Assert.Equal("export NAME='<your-key>'", line);
		Assert.Equal(Strings.UnknownShellStartupFile, startupFile);
	}

	[Fact]
	public void Fish_PrintsRunOnceInstruction()
	{
		_io.ReadKeys.Enqueue(ConsoleKey.Enter);
		_io.ReadLines.Enqueue("");
		_io.ReadKeys.Enqueue(ConsoleKey.Enter);

		Run(shell: "fish");

		Assert.Contains(
			"set -Ux ANTHROPIC_API_KEY_SHELLY '<your-key>'",
			Output);
		Assert.Contains("Fish remembers it", Output);
	}
}
