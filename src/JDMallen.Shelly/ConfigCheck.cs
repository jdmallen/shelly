namespace JDMallen.Shelly;

/// <summary>
/// What the active provider still needs before shelly can ask it anything:
/// the same conditions <see cref="ProviderFactory" /> fails on, reported as
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

		switch (config.Kind)
		{
			case ProviderKind.Anthropic:
				AddIfUnset(
					missing,
					getEnv,
					ChatProvider.ANTHROPIC_API_KEY_ENV_VAR);

				break;
			case ProviderKind.OpenAI:
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
				AddIf(
					missing,
					openAI.TimeoutSeconds <= 0,
					"openai.timeoutSeconds");
				AddIf(missing, openAI.MaxTokens <= 0, "openai.maxTokens");

				break;
			case ProviderKind.Azure:
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
				AddIf(
					missing,
					azure.TimeoutSeconds <= 0,
					"azure.timeoutSeconds");
				AddAzureAuth(missing, azure, getEnv);

				break;
			default:
				missing.Add(PROVIDER_KEY);

				break;
		}

		return missing;
	}

	/// <summary>
	/// The key variable for <c>apiKey</c>; for <c>azureCli</c>, valid tenant and
	/// scope settings and an <c>az</c> on PATH; for <c>azureIdentity</c>, a
	/// known credential and valid tenant and scope. Whether anyone is signed in
	/// isn't checked: that would start a process or touch the network, and a
	/// browser sign-in happens on the first request anyway.
	/// </summary>
	private static void AddAzureAuth(
		List<string> missing,
		AzureConfig azure,
		Func<string, string?> getEnv)
	{
		AzureAuthKind? auth = azure.AuthKind;
		switch (auth)
		{
			case null:
				missing.Add("azure.auth");

				return;
			case AzureAuthKind.ApiKey:
				AddIfUnset(missing, getEnv, ChatProvider.AZURE_API_KEY_ENV_VAR);

				return;
		}

		// Both Entra ID methods take a tenant and scope; each has one more need.
		AddIf(
			missing,
			auth == AzureAuthKind.AzureIdentity && azure.CredentialKind is null,
			"azure.credential");
		if (azure.InvalidTokenSetting() is { } invalidKey)
		{
			missing.Add(invalidKey);
		}

		AddIf(
			missing,
			auth == AzureAuthKind.AzureCli && AzureCli.Find(getEnv) is null,
			AZURE_CLI);
	}

	/// <summary>Reported when <c>az</c> isn't on PATH.</summary>
	internal const string AZURE_CLI = "az";

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
