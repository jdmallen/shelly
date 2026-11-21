using System.Runtime.InteropServices;

namespace JDMallen.Shelly;

/// <summary>
/// Where shelly is running: the shell it was launched from, the OS and the
/// working directory. The model is told all three, and suggested commands run
/// in <see cref="Shell" />.
/// </summary>
internal sealed record ShellContext(
	Shell Shell,
	string OperatingSystem,
	string WorkingDirectory)
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
