using System.Globalization;
using System.Resources;

namespace JDMallen.Shelly;

/// <summary>
/// User-facing text, localized via <c>Resources/Strings*.resx</c>. Lookups use
/// <see cref="CultureInfo.CurrentUICulture" />, which .NET derives from
/// <c>LC_ALL</c>/<c>LC_MESSAGES</c>/<c>LANG</c> on Unix; unmatched cultures fall
/// back to the neutral (en-US) strings.
/// </summary>
internal static class Strings
{
	internal static readonly ResourceManager ResourceManager = new(
		"JDMallen.Shelly.Resources.Strings",
		typeof(Strings).Assembly);

	public static string WhatToDo => Get(nameof(WhatToDo));

	public static string Thinking => Get(nameof(Thinking));

	public static string NoSuggestion => Get(nameof(NoSuggestion));

	public static string SuggestionHeader => Get(nameof(SuggestionHeader));

	public static string Explaining => Get(nameof(Explaining));

	public static string ExplanationHeader => Get(nameof(ExplanationHeader));

	public static string CopiedToClipboard => Get(nameof(CopiedToClipboard));

	public static string InstallClipboardToolLinux => Get(nameof(InstallClipboardToolLinux));

	public static string WhatShouldBeDifferent => Get(nameof(WhatShouldBeDifferent));

	public static string Goodbye => Get(nameof(Goodbye));

	public static string InvalidChoice => Get(nameof(InvalidChoice));

	public static string MenuExecute => Get(nameof(MenuExecute));

	public static string MenuEdit => Get(nameof(MenuEdit));

	public static string MenuExplain => Get(nameof(MenuExplain));

	public static string MenuCopy => Get(nameof(MenuCopy));

	public static string MenuRetry => Get(nameof(MenuRetry));

	public static string MenuQuit => Get(nameof(MenuQuit));

	public static string Executing => Get(nameof(Executing));

	public static string FailedToStartShell => Get(nameof(FailedToStartShell));

	public static string ListConjunction => Get(nameof(ListConjunction));

	public static string NoGraphicalSession => Get(nameof(NoGraphicalSession));

	public static string NoClipboardTool => Get(nameof(NoClipboardTool));

	public static string HelpOptionHelp => Get(nameof(HelpOptionHelp));

	public static string HelpOptionVersion => Get(nameof(HelpOptionVersion));

	public static string HelpOptionInfo => Get(nameof(HelpOptionInfo));

	public static string HelpOptionTerminator => Get(nameof(HelpOptionTerminator));

	public static string HelpPrompt => Get(nameof(HelpPrompt));

	public static string HelpOptionConfig => Get(nameof(HelpOptionConfig));

	public static string MainMenuAsk => Get(nameof(MainMenuAsk));

	public static string MainMenuConfigure => Get(nameof(MainMenuConfigure));

	public static string MainMenuInfo => Get(nameof(MainMenuInfo));

	public static string MainMenuExit => Get(nameof(MainMenuExit));

	public static string MenuBackToMainMenu => Get(nameof(MenuBackToMainMenu));

	public static string DefaultMarker => Get(nameof(DefaultMarker));

	public static string ContinuePrompt => Get(nameof(ContinuePrompt));

	public static string ProviderHeader => Get(nameof(ProviderHeader));

	public static string ProviderOpenAICompatible => Get(nameof(ProviderOpenAICompatible));

	public static string SettingsHeader => Get(nameof(SettingsHeader));

	public static string ValueRequired => Get(nameof(ValueRequired));

	public static string InvalidPositiveInteger => Get(nameof(InvalidPositiveInteger));

	public static string InvalidHttpUrl => Get(nameof(InvalidHttpUrl));

	public static string ApiKeyHeader => Get(nameof(ApiKeyHeader));

	public static string ApiKeyOptional => Get(nameof(ApiKeyOptional));

	public static string EnterApiKey => Get(nameof(EnterApiKey));

	public static string Skipped => Get(nameof(Skipped));

	public static string UnknownShellStartupFile => Get(nameof(UnknownShellStartupFile));

	public static string SaveChangesPrompt => Get(nameof(SaveChangesPrompt));

	public static string ConfigNotSaved => Get(nameof(ConfigNotSaved));

	public static string InteractiveTerminalRequired => Get(nameof(InteractiveTerminalRequired));

	public static string Choose(int defaultNumber) =>
		Format("ChooseFormat", defaultNumber);

	public static string ConfigHeader(string configPath) =>
		Format("ConfigHeaderFormat", configPath);

	public static string ConfigUnreadable(string configPath, string message) =>
		Format("ConfigUnreadableFormat", configPath, message);

	public static string ApiKeySet(string variableName) =>
		Format("ApiKeySetFormat", variableName);

	public static string ApiKeyNotSet(string variableName) =>
		Format("ApiKeyNotSetFormat", variableName);

	public static string SetUserVariablePrompt(string variableName) =>
		Format("SetUserVariablePromptFormat", variableName);

	public static string UserVariableSaved(string variableName) =>
		Format("UserVariableSavedFormat", variableName);

	public static string ShellStartupLine(string startupFile, string placeholder) =>
		Format("ShellStartupLineFormat", startupFile, placeholder);

	public static string FishUniversalVariable(string placeholder) =>
		Format("FishUniversalVariableFormat", placeholder);

	public static string MainMenuNeedsSetup(string missingSettings) =>
		Format("MainMenuNeedsSetupFormat", missingSettings);

	public static string ConfigBackedUp(string backupPath) =>
		Format("ConfigBackedUpFormat", backupPath);

	public static string ConfigSaved(string configPath) =>
		Format("ConfigSavedFormat", configPath);

	public static string ConfigSaveFailed(string configPath, string message) =>
		Format("ConfigSaveFailedFormat", configPath, message);

	public static string DeprecatedEnvVar(string variableName, string configKey, string configPath) =>
		Format("DeprecatedEnvVarFormat", variableName, configKey, configPath);

	public static string Error(string message) =>
		Format("ErrorFormat", message);

	public static string ClipboardUnavailable(string message) =>
		Format("ClipboardUnavailableFormat", message);

	public static string UnknownProvider(string provider, string configPath) =>
		Format("UnknownProviderFormat", provider, configPath);

	public static string EnvVarNotSet(string variableName) =>
		Format("EnvVarNotSetFormat", variableName);

	public static string OpenAIMisconfigured(string missingKeys, string configPath) =>
		Format("OpenAIMisconfiguredFormat", missingKeys, configPath);

	public static string MustBeGreaterThanZero(string settingName, int actualValue) =>
		Format("MustBeGreaterThanZeroFormat", settingName, actualValue);

	public static string AzureMisconfigured(string missingKeys, string configPath) =>
		Format("AzureMisconfiguredFormat", missingKeys, configPath);

	public static string UnknownOption(string option) =>
		Format("UnknownOptionFormat", option);

	public static string ConfigParseWarning(string path, string message) =>
		Format("ConfigParseWarningFormat", path, message);

	public static string ProcessExited(string fileName, int exitCode, string error) =>
		Format(
				"ProcessExitedFormat",
				fileName,
				exitCode,
				error)
			.Trim();

	/// <summary>
	/// The English name of the language the model should answer in, or empty
	/// for the neutral (en-US) culture and any culture without a translation.
	/// </summary>
	public static string ResponseLanguage(CultureInfo? culture = null) =>
		Get(nameof(ResponseLanguage), culture);

	internal static string Get(string resourceKey, CultureInfo? culture = null) =>
		ResourceManager.GetString(
			resourceKey,
			culture ?? CultureInfo.CurrentUICulture)
		?? throw new InvalidOperationException(
			$"Missing string resource '{resourceKey}'.");

	private static string Format(string resourceKey, params object[] arguments) =>
		string.Format(CultureInfo.CurrentCulture, Get(resourceKey), arguments);
}
