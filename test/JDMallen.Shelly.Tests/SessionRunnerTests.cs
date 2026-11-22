using JDMallen.Shelly.Tests.Fakes;
using JDMallen.Toolbox.AI;

using Xunit;

namespace JDMallen.Shelly.Tests;

public sealed class SessionRunnerTests
{
	private readonly FakeChatProvider _provider = new();
	private readonly FakeConsoleIO _io = new();
	private readonly FakeInterruptSource _interrupts = new();
	private readonly FakeCommandRunner _commandRunner = new();
	private readonly List<long> _recordedTotals = [];

	private SessionRunner NewRunner(string? editFilePath = null)
	{
		Shell shell = Shell.FromNameOrPath("bash");
		var loop = new ReplLoop(
			_provider,
			_io,
			new FakeClipboard(),
			editFilePath is not null,
			new ShellContext(shell, "os", "/pwd"));

		return new SessionRunner(
			loop,
			shell,
			_io,
			_interrupts,
			_commandRunner,
			usage => _recordedTotals.Add(usage.TotalTokens),
			editFilePath);
	}

	private void SuggestLsSpending15Tokens()
	{
		_provider.SuggestionResponses.Enqueue("ls -la");
		_provider.SuggestionUsages.Enqueue(new TokenUsage(10, 5));
	}

	[Fact]
	public async Task Quit_RecordsUsageOnce()
	{
		SuggestLsSpending15Tokens();
		_io.ReadKeys.Enqueue(ConsoleKey.Q);

		int exitCode = await NewRunner()
			.RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.Equal(0, exitCode);
		Assert.Equal([15], _recordedTotals);
	}

	[Fact]
	public async Task CtrlCAtTheMenu_RecordsUsageAndLetsShellyDie()
	{
		SuggestLsSpending15Tokens();
		bool? keepRunning = null;
		_io.BeforeReadKey = () => keepRunning = _interrupts.Interrupt();

		// A real Ctrl+C ends the process here; the fake lets the loop go on
		// so the test can check nothing is recorded twice.
		_io.ReadKeys.Enqueue(ConsoleKey.Q);

		await NewRunner()
			.RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.False(keepRunning);
		Assert.Equal([15], _recordedTotals);
	}

	[Fact]
	public async Task CtrlCWhileExecuting_KeepsShellyAndReturnsTheChildsCode()
	{
		SuggestLsSpending15Tokens();
		_io.ReadKeys.Enqueue(ConsoleKey.X);
		bool? keepRunning = null;
		_commandRunner.WhileRunning =
			() => keepRunning = _interrupts.Interrupt();
		_commandRunner.ExitCode = 130;

		int exitCode = await NewRunner()
			.RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.True(keepRunning);
		Assert.Equal(130, exitCode);
		Assert.Equal(["ls -la"], _commandRunner.Commands);
		Assert.Equal([15], _recordedTotals);
	}

	[Fact]
	public async Task Execute_RecordsUsageBeforeTheCommandRuns()
	{
		SuggestLsSpending15Tokens();
		_io.ReadKeys.Enqueue(ConsoleKey.X);
		int recordedBeforeRun = -1;
		_commandRunner.WhileRunning =
			() => recordedBeforeRun = _recordedTotals.Count;
		_commandRunner.ExitCode = 3;

		int exitCode = await NewRunner()
			.RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.Equal(3, exitCode);
		Assert.Equal(1, recordedBeforeRun);
	}

	[Fact]
	public async Task ShellFailsToStart_ReportsItAndReturns1()
	{
		SuggestLsSpending15Tokens();
		_io.ReadKeys.Enqueue(ConsoleKey.X);
		_commandRunner.ExitCode = null;

		int exitCode = await NewRunner()
			.RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.Equal(1, exitCode);
		Assert.Contains(Strings.FailedToStartShell, _io.Errors.ToString());
	}

	[Fact]
	public async Task Edit_WritesTheCommandForTheWrapper()
	{
		string directory =
			Directory.CreateTempSubdirectory("shelly-edit-").FullName;
		try
		{
			string path = Path.Combine(directory, "edit");
			SuggestLsSpending15Tokens();
			_io.ReadKeys.Enqueue(ConsoleKey.E);

			int exitCode = await NewRunner(path)
				.RunAsync("list files", TestContext.Current.CancellationToken);

			Assert.Equal(0, exitCode);
			Assert.Equal(
				"ls -la",
				await File.ReadAllTextAsync(
					path,
					TestContext.Current.CancellationToken));
		}
		finally
		{
			Directory.Delete(directory, true);
		}
	}

	[Fact]
	public async Task Edit_UnwritableFile_ReportsItAndPrintsTheCommand()
	{
		string path = Path.Combine(
			Path.GetTempPath(),
			$"shelly-missing-{Guid.NewGuid():N}",
			"edit");
		SuggestLsSpending15Tokens();
		_io.ReadKeys.Enqueue(ConsoleKey.E);

		int exitCode = await NewRunner(path)
			.RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.Equal(1, exitCode);
		Assert.StartsWith(Strings.Error(string.Empty), _io.Errors.ToString());
		Assert.EndsWith(
			$"ls -la{Environment.NewLine}",
			_io.Output.ToString());
	}

	[Fact]
	public async Task Finished_StopsHandlingCtrlC()
	{
		SuggestLsSpending15Tokens();
		_io.ReadKeys.Enqueue(ConsoleKey.Q);

		await NewRunner()
			.RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.False(_interrupts.IsSubscribed);
	}
}
