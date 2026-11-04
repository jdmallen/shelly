using System.Text;

namespace JDMallen.Shelly;

/// <summary>
/// The <c>--info</c> report: which provider shelly would use and where it
/// would send requests. Diagnostic output, so it stays in English like
/// <c>--version</c>. API keys are reported as set or not set, never shown.
/// </summary>
internal static class InfoReport
{
	internal const string ANTHROPIC_ENDPOINT = "https://api.anthropic.com/v1/messages";

	public static string Build(
		string versionLine,
		string configPath,
		ShellyConfig config,
		Shell shell,
		Func<string, string?> getEnvironmentVariable)
	{
		var rows = new List<(string Label, string Value)>
		{
			("config", configPath),
			("provider", config.Provider),
		};

		switch (config.Provider.ToLowerInvariant())
		{
			case ShellyConfig.AZURE_PROVIDER:
				rows.Add((
					"endpoint",
					Describe(config.Azure.ResolveEndpoint(getEnvironmentVariable), "azure.endpoint")));
				rows.Add((
					"deployment",
					Describe(config.Azure.ResolveDeployment(getEnvironmentVariable), "azure.deployment")));
				rows.Add(("reasoning", config.Azure.ReasoningModel ? "yes (temperature not sent)" : "no"));
				rows.Add(("max tokens", config.Azure.MaxTokens.ToString()));
				rows.Add(("api key", KeyStatus(ChatProvider.AzureApiKeyEnvVar, getEnvironmentVariable)));

				break;
			case ShellyConfig.ANTHROPIC_PROVIDER:
				rows.Add(("endpoint", ANTHROPIC_ENDPOINT));
				rows.Add(("model", config.Anthropic.Model));
				rows.Add(("max tokens", ChatProvider.DefaultMaxTokens.ToString()));
				rows.Add(("api key", KeyStatus(ChatProvider.AnthropicApiKeyEnvVar, getEnvironmentVariable)));

				break;
			case ShellyConfig.OPENAI_PROVIDER or ShellyConfig.LOCAL_PROVIDER_ALIAS:
				rows.Add(("endpoint", OrNotSet(config.OpenAI.BaseUrl, "openai.baseUrl")));
				rows.Add(("model", OrNotSet(config.OpenAI.Model, "openai.model")));
				rows.Add(("max tokens", config.OpenAI.MaxTokens.ToString()));
				rows.Add(("timeout", $"{config.OpenAI.TimeoutSeconds}s"));
				rows.Add((
					"api key",
					$"{KeyStatus(ChatProvider.OpenAIApiKeyEnvVar, getEnvironmentVariable)} (optional)"));

				break;
			default:
				rows.Add(("error", "unknown provider; expected anthropic, azure, openai or local"));

				break;
		}

		rows.Add(("shell", $"{shell.DisplayName} ({string.Join(' ', [shell.Executable, .. shell.CommandFlags])})"));

		int labelWidth = rows.Max(row => row.Label.Length) + 1;
		var report = new StringBuilder(versionLine).AppendLine().AppendLine();
		foreach ((string label, string value) in rows)
		{
			report.AppendLine($"  {$"{label}:".PadRight(labelWidth)} {value}");
		}

		return report.ToString().TrimEnd();
	}

	private static string Describe(AzureSetting setting, string configKey) =>
		!setting.IsSet
			? $"(not set: {configKey})"
			: setting.DeprecatedVariable is null
				? setting.Value!
				: $"{setting.Value} (deprecated: from {setting.DeprecatedVariable})";

	private static string OrNotSet(string value, string setting) =>
		string.IsNullOrWhiteSpace(value) ? $"(not set: {setting})" : value;

	private static string KeyStatus(string variable, Func<string, string?> getEnvironmentVariable) =>
		string.IsNullOrEmpty(getEnvironmentVariable(variable))
			? $"{variable} is not set"
			: $"{variable} is set";
}
