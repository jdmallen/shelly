namespace JDMallen.Shelly.Tests.Fakes;

internal sealed class FakeCommandRunner : ICommandRunner
{
	public List<string> Commands { get; } = [];

	/// <summary>
	/// The exit code to report; null means the shell didn't start.
	/// </summary>
	public int? ExitCode { get; set; }

	/// <summary>Runs while the "command" is running, e.g. a Ctrl+C.</summary>
	public Action? WhileRunning { get; set; }

	public Task<int?> RunAsync(string command, Shell shell)
	{
		Commands.Add(command);
		WhileRunning?.Invoke();

		return Task.FromResult(ExitCode);
	}
}
