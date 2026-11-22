using System.Text;

namespace JDMallen.Shelly;

internal interface IConsoleIO
{
	/// <summary>
	/// False when stdin or stdout is redirected. With stdin piped or from a
	/// file there is no keyboard to read single keys from; with stdout
	/// captured (e.g. <c>$(shelly "...")</c>) nobody would see the menu. Either
	/// way the REPL prints the suggestion and exits instead.
	/// </summary>
	bool IsInteractive { get; }

	/// <summary>
	/// True when stdin is piped or from a file. Distinguishes
	/// <c>x=$(shelly)</c>, where someone is still typing at a keyboard, from
	/// <c>echo "..." | shelly</c>, where nobody is.
	/// </summary>
	bool IsInputRedirected { get; }

	/// <summary>
	/// True when stdout is piped or captured: what's written is data for
	/// another program, not text for someone to read.
	/// </summary>
	bool IsOutputRedirected { get; }

	void Write(string text, ConsoleColor? color = null);

	void WriteLine(string text = "", ConsoleColor? color = null);

	/// <summary>Writes a line to stderr, uncolored.</summary>
	void WriteError(string text);

	/// <summary>
	/// Writes to stderr with no newline, uncolored: a question asked while
	/// stdout is captured.
	/// </summary>
	void WriteErrorInline(string text);

	string? ReadLine();

	/// <summary>Reads the rest of stdin, e.g. a whole piped prompt.</summary>
	string ReadToEnd();

	/// <summary>
	/// Shows <paramref name="message" /> and asks <paramref name="question" />
	/// on the terminal itself, for when stdin is a pipe. Returns whether the
	/// answer was yes (y or yes; anything else is no), or null when there is
	/// no terminal to ask on (cron, CI).
	/// </summary>
	bool? ConfirmOnTerminal(string message, string question);

	ConsoleKey ReadKey();

	/// <summary>
	/// Reads a line without showing it: each character echoes as <c>*</c>.
	/// Returns null if the user presses Esc.
	/// </summary>
	string? ReadSecret();
}

internal sealed class SystemConsoleIO : IConsoleIO
{
	public bool IsInteractive =>
		!Console.IsInputRedirected && !Console.IsOutputRedirected;

	public bool IsInputRedirected => Console.IsInputRedirected;

	public bool IsOutputRedirected => Console.IsOutputRedirected;

	public void Write(string text, ConsoleColor? color = null)
	{
		if (color is null)
		{
			Console.Write(text);

			return;
		}

		ConsoleColor previous = Console.ForegroundColor;
		Console.ForegroundColor = color.Value;
		Console.Write(text);
		Console.ForegroundColor = previous;
	}

	// The newline is written uncolored, so a color never bleeds into the
	// next line on terminals that paint to the end of the line.
	public void WriteLine(string text = "", ConsoleColor? color = null)
	{
		Write(text, color);
		Console.WriteLine();
	}

	public void WriteError(string text) => Console.Error.WriteLine(text);

	public void WriteErrorInline(string text) => Console.Error.Write(text);

	public string? ReadLine() => Console.ReadLine();

	public string ReadToEnd() => Console.In.ReadToEnd();

	public bool? ConfirmOnTerminal(string message, string question)
	{
		// stdin is the pipe, so ask the controlling terminal directly.
		(string inputPath, string outputPath) = OperatingSystem.IsWindows()
			? ("CONIN$", "CONOUT$")
			: ("/dev/tty", "/dev/tty");
		try
		{
			using var input = new StreamReader(
				new FileStream(inputPath, FileMode.Open, FileAccess.Read));
			using var output = new StreamWriter(
				new FileStream(outputPath, FileMode.Open, FileAccess.Write));
			output.AutoFlush = true;
			output.WriteLine(message);
			output.Write($"{question} [y/N] ");
			string? answer = input.ReadLine()?.Trim().ToUpperInvariant();

			return answer is "Y" or "YES";
		}
		catch (Exception ex) when (ex is IOException
									   or UnauthorizedAccessException)
		{
			// No controlling terminal: opening /dev/tty fails with ENXIO.
			return null;
		}
	}

	public ConsoleKey ReadKey() => Console.ReadKey(true).Key;

	public string? ReadSecret()
	{
		var secret = new StringBuilder();
		while (true)
		{
			ConsoleKeyInfo keyInfo = Console.ReadKey(true);

			// ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
			switch (keyInfo.Key)
			{
				case ConsoleKey.Enter:
					Console.WriteLine();

					return secret.ToString();
				case ConsoleKey.Escape:
					Console.WriteLine();

					return null;
				case ConsoleKey.Backspace:
					if (secret.Length > 0)
					{
						secret.Length--;
						Console.Write(@" ");
					}

					break;
				default:
					if (!char.IsControl(keyInfo.KeyChar))
					{
						secret.Append(keyInfo.KeyChar);
						Console.Write('*');
					}

					break;
			}
		}
	}
}
