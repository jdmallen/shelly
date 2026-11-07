using System.Globalization;
using System.Text;

namespace JDMallen.Shelly;

/// <summary>
/// The <c>--info</c> report: which provider shelly would use and where it
/// would send requests, plus lifetime token usage. Diagnostic output, so it
/// stays in English like <c>--version</c>. API keys are reported as set or not
/// set, never shown.
/// </summary>
internal static class InfoReport
{
	internal const string ANTHROPIC_ENDPOINT =
		"https://api.anthropic.com/v1/messages";

	public static string Build(
		string versionLine,
		string configPath,
		ShellyConfig config,
		Shell shell,
		Func<string, string?> getEnvironmentVariable,
		UsageReport? usage = null)
	{
		var rows = new List<(string Label, string Value)>
		{
			("config", configPath),
			("provider", config.Provider),
		};

		switch (config.Provider.ToLowerInvariant())
		{
			case ShellyConfig.AZURE_PROVIDER:
				rows.Add(
					(
						"endpoint",
						Describe(
							config.Azure.ResolveEndpoint(
								getEnvironmentVariable),
							"azure.endpoint")));
				rows.Add(
					(
						"deployment",
						Describe(
							config.Azure.ResolveDeployment(
								getEnvironmentVariable),
							"azure.deployment")));
				rows.Add(
					("reasoning",
						config.Azure.ReasoningModel
							? "yes (temperature not sent)"
							: "no"));
				rows.Add(
					("max tokens",
						config.Azure.MaxTokens.ToString(
							CultureInfo.CurrentCulture)));
				rows.Add(
					("api key",
						KeyStatus(
							ChatProvider.AZURE_API_KEY_ENV_VAR,
							getEnvironmentVariable)));

				break;
			case ShellyConfig.ANTHROPIC_PROVIDER:
				rows.Add(("endpoint", ANTHROPIC_ENDPOINT));
				rows.Add(("model", config.Anthropic.Model));
				rows.Add(
					("max tokens",
						ChatProvider.DEFAULT_MAX_TOKENS.ToString(
							CultureInfo.CurrentCulture)));
				rows.Add(
					("api key",
						KeyStatus(
							ChatProvider.ANTHROPIC_API_KEY_ENV_VAR,
							getEnvironmentVariable)));

				break;
			case ShellyConfig.OPENAI_PROVIDER
				or ShellyConfig.LOCAL_PROVIDER_ALIAS:
				rows.Add(
					("endpoint",
						OrNotSet(config.OpenAI.BaseUrl, "openai.baseUrl")));
				rows.Add(
					("model", OrNotSet(config.OpenAI.Model, "openai.model")));
				rows.Add(
					("max tokens",
						config.OpenAI.MaxTokens.ToString(
							CultureInfo.CurrentCulture)));
				rows.Add(("timeout", $"{config.OpenAI.TimeoutSeconds}s"));
				rows.Add(
					(
						"api key",
						$"{KeyStatus(ChatProvider.OPEN_AI_API_KEY_ENV_VAR, getEnvironmentVariable)} (optional)"));

				break;
			default:
				rows.Add(
					("error",
						"unknown provider; expected anthropic, azure, openai or local"));

				break;
		}

		rows.Add(
			("shell",
				$"{shell.DisplayName} ({string.Join(' ', [shell.Executable, .. shell.CommandFlags])})"));

		StringBuilder report =
			new StringBuilder(versionLine).AppendLine().AppendLine();
		AppendRows(report, rows);

		if (usage is not null)
		{
			report.AppendLine();
			AppendRows(report, UsageRows(usage.Value));
		}

		return report.ToString().TrimEnd();
	}

	private static void AppendRows(
		StringBuilder report,
		List<(string Label, string Value)> rows)
	{
		int labelWidth = rows.Max(row => row.Label.Length) + 1;
		foreach ((string label, string value) in rows)
		{
			report.AppendLine(
				CultureInfo.CurrentCulture,
				$"  {$"{label}:".PadRight(labelWidth)} {value}");
		}
	}

	private static List<(string Label, string Value)> UsageRows(
		UsageReport usage)
	{
		var rows = new List<(string Label, string Value)>
		{
			("usage log", usage.Path),
		};

		if (usage.Totals is not { } totals)
		{
			rows.Add(("total", $"(unreadable: {usage.Error})"));

			return rows;
		}

		if (totals.Runs == 0)
		{
			rows.Add(("total", "none recorded yet"));

			return rows;
		}

		string since = totals.Since is { } sinceDate
			? $" since {sinceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
			: string.Empty;
		string runs = totals.Runs == 1
			? "1 run"
			: $"{Count(totals.Runs)} runs";
		rows.Add(
			("total",
				$"{Count(totals.TotalTokens)} tokens ({Count(totals.InputTokens)} in, {Count(totals.OutputTokens)} out) over {runs}{since}"));
		rows.Add(
			("average",
				$"{Average(totals.TotalTokens)} tokens per run ({Average(totals.InputTokens)} in, {Average(totals.OutputTokens)} out)"));

		return rows;

		string Average(long tokens) =>
			Count(
				(long)Math.Round(
					(double)tokens / totals.Runs,
					MidpointRounding.AwayFromZero));
	}

	private static string Count(long value) =>
		value.ToString("N0", CultureInfo.CurrentCulture);

	/// <summary>The usage block's inputs.</summary>
	/// <param name="Path">Where the totals live.</param>
	/// <param name="Totals">The totals, or null when the file is unreadable.</param>
	/// <param name="Error">Why the file is unreadable, when it is.</param>
	internal readonly record struct UsageReport(
		string Path,
		UsageTotals? Totals,
		string? Error);

	private static string Describe(AzureSetting setting, string configKey) =>
		!setting.IsSet
			? $"(not set: {configKey})"
			: setting.DeprecatedVariable is null
				? setting.Value!
				: $"{setting.Value} (deprecated: from {setting.DeprecatedVariable})";

	private static string OrNotSet(string value, string setting) =>
		string.IsNullOrWhiteSpace(value) ? $"(not set: {setting})" : value;

	private static string KeyStatus(
		string variable,
		Func<string, string?> getEnvironmentVariable) =>
		string.IsNullOrEmpty(getEnvironmentVariable(variable))
			? $"{variable} is not set"
			: $"{variable} is set";
}
