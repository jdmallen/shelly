using System.Runtime.InteropServices;

namespace JDMallen.Shelly;

/// <summary>
/// Where shelly is running: the shell it was launched from, the OS and the
/// working directory. The model is told all three, and suggested commands run
/// in <see cref="Shell" />.
/// </summary>
public sealed record ShellContext(Shell Shell, string OperatingSystem, string WorkingDirectory)
{
	public static ShellContext Detect() =>
		new(
			ShellDetector.Detect(),
			RuntimeInformation.OSDescription,
			Directory.GetCurrentDirectory());

	/// <summary>The one-line context given to the model.</summary>
	public string Describe() =>
		$"Shell: {Shell.DisplayName}, OS: {OperatingSystem}, PWD: {WorkingDirectory}";
}

/// <summary>
/// Finds the shell shelly was launched from. <c>$SHELL</c> alone is not
/// enough: it names the login shell, so running <c>bash</c> or <c>fish</c>
/// from a zsh login still reports zsh, and Windows has no equivalent at all.
/// In order:
/// <list type="number">
/// <item><see cref="OVERRIDE_ENV_VAR" />, when set.</item>
/// <item>The nearest ancestor process that is a known shell. The walk skips
/// non-shells in between, such as <c>sudo</c>, <c>env</c> or
/// <c>dotnet run</c>.</item>
/// <item><c>$SHELL</c> on Unix, <c>%ComSpec%</c> on Windows.</item>
/// </list>
/// </summary>
internal static class ShellDetector
{
	internal const string OVERRIDE_ENV_VAR = "SHELLY_SHELL";

	private const int MAX_ANCESTORS = 16;
	private const string UNIX_FALLBACK_SHELL = "/bin/sh";
	private const string WINDOWS_FALLBACK_SHELL = "cmd.exe";

	public static Shell Detect() =>
		Detect(
			Environment.GetEnvironmentVariable,
			ProcessTree.ForCurrentPlatform(),
			Environment.ProcessId,
			System.OperatingSystem.IsWindows());

	internal static Shell Detect(
		Func<string, string?> getEnvironmentVariable,
		IProcessTree processTree,
		int processId,
		bool isWindows)
	{
		string? overrideValue = getEnvironmentVariable(OVERRIDE_ENV_VAR);
		if (!string.IsNullOrWhiteSpace(overrideValue))
		{
			return Shell.FromNameOrPath(overrideValue.Trim());
		}

		return FromAncestors(processTree, processId)
			?? FromEnvironment(getEnvironmentVariable, isWindows);
	}

	private static Shell? FromAncestors(IProcessTree processTree, int processId)
	{
		var visited = new HashSet<int> { processId };
		int ancestorId = processTree.Get(processId)?.ParentId ?? 0;

		for (var depth = 0; depth < MAX_ANCESTORS && ancestorId > 0 && visited.Add(ancestorId); depth++)
		{
			ProcessEntry? ancestor = processTree.Get(ancestorId);
			if (ancestor is null)
			{
				return null;
			}

			Shell? shell = Shell.TryResolveKnown(ancestor.Value.Name, ancestor.Value.ExecutablePath);
			if (shell is null && !string.IsNullOrEmpty(ancestor.Value.ExecutablePath))
			{
				shell = Shell.TryResolveKnown(
					ancestor.Value.ExecutablePath,
					ancestor.Value.ExecutablePath);
			}

			if (shell is not null)
			{
				return shell;
			}

			ancestorId = ancestor.Value.ParentId;
		}

		return null;
	}

	private static Shell FromEnvironment(Func<string, string?> getEnvironmentVariable, bool isWindows)
	{
		string? configured = getEnvironmentVariable(isWindows ? "ComSpec" : "SHELL");

		return Shell.FromNameOrPath(
			string.IsNullOrWhiteSpace(configured)
				? isWindows ? WINDOWS_FALLBACK_SHELL : UNIX_FALLBACK_SHELL
				: configured.Trim());
	}
}
