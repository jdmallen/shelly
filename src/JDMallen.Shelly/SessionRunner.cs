namespace JDMallen.Shelly;

/// <summary>
/// One ask: runs the loop, records the tokens it spent, then executes or
/// hands off the accepted command. Owns Ctrl+C for the whole run.
/// </summary>
/// <param name="recordUsage">
/// Adds the session's usage to the lifetime log. Called exactly once, either
/// when the loop returns or on a Ctrl+C before that.
/// </param>
/// <param name="editFilePath">
/// Where [e]dit writes the command for the shell wrapper
/// (<see cref="EditHandoff.FilePath" />); null without the wrapper.
/// </param>
internal sealed class SessionRunner(
	ReplLoop loop,
	Shell shell,
	IConsoleIO io,
	IInterruptSource interrupts,
	ICommandRunner commandRunner,
	Action<TokenUsageTally> recordUsage,
	string? editFilePath)
{
	private int _usageRecorded;
	private volatile bool _executing;

	public async Task<int> RunAsync(
		string? prompt,
		CancellationToken cancellationToken = default)
	{
		using IDisposable subscription = interrupts.Subscribe(OnInterrupt);

		ReplResult result = await loop.RunAsync(prompt, cancellationToken);

		// Before executing: the command may run long, and the tokens were
		// spent either way.
		RecordUsageOnce();

		if (result.Action == ReplAction.Failed)
		{
			return 1;
		}

		if (string.IsNullOrWhiteSpace(result.Command))
		{
			return 0;
		}

		return result.Action switch
		{
			ReplAction.Execute => await ExecuteAsync(result.Command),
			ReplAction.Edit    => WriteEditHandoff(result.Command),
			_                  => 0,
		};
	}

	private bool OnInterrupt()
	{
		if (_executing)
		{
			// The terminal sent SIGINT to the child too. Stay alive so the
			// child keeps the terminal to itself and its exit code survives.
			return true;
		}

		// Leaving at a prompt or the menu: keep the tokens already spent,
		// then let the interrupt end shelly as usual (the shell sees 130).
		RecordUsageOnce();

		return false;
	}

	private void RecordUsageOnce()
	{
		if (Interlocked.Exchange(ref _usageRecorded, 1) == 0)
		{
			loop.ReadUsage(recordUsage);
		}
	}

	private async Task<int> ExecuteAsync(string command)
	{
		io.WriteLine(Strings.Executing, ConsoleColor.Cyan);

		_executing = true;
		int? exitCode = await commandRunner.RunAsync(command, shell);
		if (exitCode is null)
		{
			io.WriteError(Strings.Error(Strings.FailedToStartShell));

			return 1;
		}

		return exitCode.Value;
	}

	private int WriteEditHandoff(string command)
	{
		if (string.IsNullOrEmpty(editFilePath))
		{
			// The edit action is only offered when the wrapper sets the env
			// var, so this is defensive: print the command so it isn't lost.
			io.WriteLine(command);

			return 0;
		}

		try
		{
			File.WriteAllText(editFilePath, command);
		}
		catch (Exception ex) when (ex is IOException
									   or UnauthorizedAccessException)
		{
			// E.g. the wrapper's temp file was removed. Still hand over the
			// command, on stdout, so it isn't lost.
			io.WriteError(TerminalText.Escape(Strings.Error(ex.Message)));
			io.WriteLine(command);

			return 1;
		}

		return 0;
	}
}
