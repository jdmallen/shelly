namespace JDMallen.Shelly;

/// <summary>
/// Coordinates the "edit" action with the shell wrapper function. A child
/// process can't inject text into its parent shell's input line, so when shelly
/// is launched through the provided shell wrapper, the wrapper creates a temp
/// file and passes its path in <see cref="FILE_ENV_VAR"/>. Choosing [e]dit writes
/// the command there and exits; the wrapper then pushes it onto the next prompt
/// (zsh <c>print -z</c>). The edit option is offered only when the wrapper is
/// active — i.e. when this env var is set.
/// </summary>
internal static class EditHandoff
{
	private const string FILE_ENV_VAR = "SHELLY_EDIT_FILE";

	public static string? FilePath => Environment.GetEnvironmentVariable(FILE_ENV_VAR);

	public static bool IsEnabled => !string.IsNullOrEmpty(FilePath);
}
