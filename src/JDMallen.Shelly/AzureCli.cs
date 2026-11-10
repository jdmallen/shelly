namespace JDMallen.Shelly;

/// <summary>
/// Facts about the Azure CLI that need no <c>az</c> process: where it is, and
/// the sign-in command for a config. Fetching a token happens in the toolbox's
/// <see cref="AzureCliTokenProvider" />, on the first request.
/// </summary>
internal static class AzureCli
{
	internal const string INSTALL_URL =
		"https://learn.microsoft.com/cli/azure/install-azure-cli";

	internal const string DATA_PLANE_ROLE = "Cognitive Services OpenAI User";

	// On Windows, az is az.cmd (a batch wrapper); the toolbox runs it via
	// cmd.exe, which resolves it the same way.
	private static readonly string[] WindowsNames =
		["az.cmd", "az.exe", "az.bat"];

	private static readonly string[] UnixNames = ["az"];

	/// <summary>
	/// The first <c>az</c> on <c>PATH</c>, as the toolbox would launch it, or
	/// null when there is none.
	/// </summary>
	public static string? Find(
		Func<string, string?> getEnvironmentVariable,
		bool? isWindows = null)
	{
		string? path = getEnvironmentVariable("PATH");
		if (string.IsNullOrEmpty(path))
		{
			return null;
		}

		string[] names = isWindows ?? OperatingSystem.IsWindows()
			? WindowsNames
			: UnixNames;

		return path
			.Split(
				Path.PathSeparator,
				StringSplitOptions.RemoveEmptyEntries
				| StringSplitOptions.TrimEntries)
			.SelectMany(
				_ => names,
				Path.Combine)
			.FirstOrDefault(File.Exists);
	}

	/// <summary>
	/// <c>az login</c> for <paramref name="azure" />'s scope and tenant, so the
	/// cached sign-in covers the token shelly asks for.
	/// </summary>
	public static string SignInCommand(AzureConfig azure)
	{
		AzureCliTokenProviderOptions options = azure.TokenProviderOptions();

		return options.TenantId is null
			? $"az login --scope {options.Scope}"
			: $"az login --tenant {options.TenantId} --scope {options.Scope}";
	}
}
