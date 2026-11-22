using System.ComponentModel;
using System.Diagnostics;

namespace JDMallen.Shelly;

/// <summary>Runs an accepted suggestion in a shell.</summary>
internal interface ICommandRunner
{
	/// <summary>
	/// Runs <paramref name="command" /> and waits for it. Returns its exit
	/// code, or null when the shell couldn't be started.
	/// </summary>
	Task<int?> RunAsync(string command, Shell shell);
}

// Runs in the shell the command was written for, i.e. the one shelly was
// launched from, not $SHELL: a fish command fails under zsh.
internal sealed class SystemCommandRunner : ICommandRunner
{
	public async Task<int?> RunAsync(string command, Shell shell)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = shell.Executable,
			UseShellExecute = false,
		};
		foreach (string flag in shell.CommandFlags)
		{
			startInfo.ArgumentList.Add(flag);
		}

		startInfo.ArgumentList.Add(command);

		Process? process;
		try
		{
			process = Process.Start(startInfo);
		}
		catch (Win32Exception)
		{
			// The shell isn't on PATH, e.g. a mistyped SHELLY_SHELL.
			return null;
		}

		if (process is null)
		{
			return null;
		}

		using (process)
		{
			await process.WaitForExitAsync();

			// A child killed by a signal reports 128 + N, e.g. 130 for SIGINT.
			return process.ExitCode;
		}
	}
}
