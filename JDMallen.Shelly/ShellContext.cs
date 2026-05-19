using System.Runtime.InteropServices;

namespace JDMallen.Shelly;

public static class ShellContext
{
	public static string Build()
	{
		string? shellPath = Environment.GetEnvironmentVariable("SHELL");
		string shell = string.IsNullOrEmpty(shellPath) ? "unknown" : Path.GetFileName(shellPath);
		string os = RuntimeInformation.OSDescription;
		string pwd = Directory.GetCurrentDirectory();

		return $"Shell: {shell}, OS: {os}, PWD: {pwd}";
	}
}
