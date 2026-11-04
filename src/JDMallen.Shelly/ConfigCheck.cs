namespace JDMallen.Shelly;

/// <summary>
/// What the active provider still needs before shelly can ask it anything:
/// the same conditions <c>Program.CreateProvider</c> fails on, reported as
/// the config keys and environment variable names to fix, without printing
/// anything. The main menu shows these next to "Configure".
/// </summary>
internal static class ConfigCheck
{
	internal const string CONFIG_FILE = "config.json";

	internal const string PROVIDER_KEY = "provider";

	/// <param name="config">The configuration to check.</param>
	/// <param name="parseError">
	/// The parser's message when <c>config.json</c> couldn't be read, else null.
	/// </param>
	/// <param name="getEnv">A function to retrieve environment variables.</param>
	/// <returns>Names to fix, in prompt order; empty when ready.</returns>
	public static IReadOnlyList<string> FindMissing(
		ShellyConfig config,
		string? parseError,
		Func<string, string?> getEnv)
	{
		var missing = new List<string>();
		if (parseError is not null)
		{
			missing.Add(CONFIG_FILE);
		}

		string provider = config.Provider.ToLowerInvariant();
		if (provider == ShellyConfig.ANTHROPIC_PROVIDER)
		{
			AddIfUnset(missing, getEnv, ChatProvider.ANTHROPIC_API_KEY_ENV_VAR);
		}
		else if (ShellyConfig.IsOpenAIProvider(provider))
		{
			// The key is optional here: self-hosted runners rarely check one.
			OpenAIConfig openAI = config.OpenAI;
			AddIf(
				missing,
				string.IsNullOrWhiteSpace(openAI.BaseUrl),
				"openai.baseUrl");
			AddIf(
				missing,
				string.IsNullOrWhiteSpace(openAI.Model),
				"openai.model");
			AddIf(missing, openAI.TimeoutSeconds <= 0, "openai.timeoutSeconds");
			AddIf(missing, openAI.MaxTokens <= 0, "openai.maxTokens");
		}
		else if (provider == ShellyConfig.AZURE_PROVIDER)
		{
			AzureConfig azure = config.Azure;

			// A value still coming from a deprecated variable works, but
			// belongs in the config file; saving the wizard once moves it.
			AddIf(
				missing,
				NeedsMove(azure.ResolveEndpoint(getEnv)),
				"azure.endpoint");
			AddIf(
				missing,
				NeedsMove(azure.ResolveDeployment(getEnv)),
				"azure.deployment");
			AddIf(missing, azure.MaxTokens <= 0, "azure.maxTokens");
			AddIfUnset(missing, getEnv, ChatProvider.AZURE_API_KEY_ENV_VAR);
		}
		else
		{
			missing.Add(PROVIDER_KEY);
		}

		return missing;
	}

	private static bool NeedsMove(AzureSetting setting) =>
		!setting.IsSet || setting.DeprecatedVariable is not null;

	private static void AddIfUnset(
		List<string> missing,
		Func<string, string?> getEnv,
		string variable) =>
		AddIf(missing, string.IsNullOrEmpty(getEnv(variable)), variable);

	private static void AddIf(List<string> missing, bool condition, string name)
	{
		if (condition && !missing.Contains(name))
		{
			missing.Add(name);
		}
	}
}
