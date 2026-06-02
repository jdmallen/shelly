using System.Diagnostics;

namespace JDMallen.Shelly;

internal interface IClipboard
{
	/// <summary>
	/// Whether a usable clipboard exists in this environment. False on a
	/// headless/SSH session (no graphical display) or when no clipboard tool is
	/// installed, so the menu can hide the copy option instead of offering one
	/// that would only fail.
	/// </summary>
	bool IsAvailable { get; }

	Task SetTextAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>
/// Copies text to the system clipboard by shelling out to a platform-native
/// tool. On Linux this probes for a Wayland (<c>wl-copy</c>) or X11
/// (<c>xclip</c>/<c>xsel</c>) helper at runtime, rather than assuming any one
/// is installed.
/// </summary>
internal sealed class SystemClipboard : IClipboard
{
	public bool IsAvailable
	{
		get
		{
			if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
			{
				return true;
			}

			(bool hasWayland, bool hasX11) = DetectDisplays();
			if (!hasWayland && !hasX11)
			{
				return false;
			}

			foreach (ClipboardCommand candidate in LinuxCandidates(hasWayland, hasX11))
			{
				if (IsOnPath(candidate.FileName))
				{
					return true;
				}
			}

			return false;
		}
	}

	public async Task SetTextAsync(string text, CancellationToken cancellationToken = default)
	{
		ClipboardCommand command = ResolveCommand();
		await PipeToAsync(command, text, cancellationToken);
	}

	private static (bool Wayland, bool X11) DetectDisplays()
	{
		bool hasWayland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
		bool hasX11 = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));

		return (hasWayland, hasX11);
	}

	private static ClipboardCommand ResolveCommand()
	{
		if (OperatingSystem.IsWindows())
		{
			return new ClipboardCommand("clip", string.Empty);
		}

		if (OperatingSystem.IsMacOS())
		{
			return new ClipboardCommand("pbcopy", string.Empty);
		}

		// Linux / other Unix: the clipboard lives in the GUI session. Without a
		// Wayland/X11 display (e.g. over a bare SSH connection) there is nothing
		// to copy into, and the helpers would block trying to reach a compositor,
		// so bail out early with a clear message.
		(bool hasWayland, bool hasX11) = DetectDisplays();
		if (!hasWayland && !hasX11)
		{
			throw new InvalidOperationException(
				"No graphical session detected (no WAYLAND_DISPLAY or DISPLAY). "
				+ "The clipboard isn't available over a headless/SSH session.");
		}

		foreach (ClipboardCommand candidate in LinuxCandidates(hasWayland, hasX11)
			         .Where(candidate => IsOnPath(candidate.FileName)))
		{
			return candidate;
		}

		throw new InvalidOperationException(
			"No clipboard tool found. Install wl-clipboard (Wayland), xclip, or xsel.");
	}

	private static IEnumerable<ClipboardCommand> LinuxCandidates(bool hasWayland, bool hasX11)
	{
		if (hasWayland)
		{
			yield return new ClipboardCommand("wl-copy", string.Empty);
		}

		if (hasX11)
		{
			yield return new ClipboardCommand("xclip", "-selection clipboard");
			yield return new ClipboardCommand("xsel", "-i --clipboard");
		}
	}

	private static bool IsOnPath(string fileName)
	{
		string? pathEnv = Environment.GetEnvironmentVariable("PATH");
		if (string.IsNullOrEmpty(pathEnv))
		{
			return false;
		}

		foreach (string dir in pathEnv.Split(Path.PathSeparator))
		{
			if (string.IsNullOrEmpty(dir))
			{
				continue;
			}

			if (File.Exists(Path.Combine(dir, fileName)))
			{
				return true;
			}
		}

		return false;
	}

	private static async Task PipeToAsync(
		ClipboardCommand command,
		string text,
		CancellationToken cancellationToken)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = command.FileName,
			Arguments = command.Arguments,
			RedirectStandardInput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		using var process = new Process();
		process.StartInfo = startInfo;
		process.Start();

		await process.StandardInput.WriteAsync(text);
		process.StandardInput.Close();

		await process.WaitForExitAsync(cancellationToken);

		if (process.ExitCode != 0)
		{
			string error = await process.StandardError.ReadToEndAsync(cancellationToken);

			throw new InvalidOperationException(
				$"'{command.FileName}' exited with code {process.ExitCode}. {error}".Trim());
		}
	}

	private readonly record struct ClipboardCommand(string FileName, string Arguments);
}
