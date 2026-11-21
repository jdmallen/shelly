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

	void Write(string text, ConsoleColor? color = null);

	void WriteLine(string text = "", ConsoleColor? color = null);

	/// <summary>Writes a line to stderr, uncolored.</summary>
	void WriteError(string text);

	string? ReadLine();

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

	public string? ReadLine() => Console.ReadLine();

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
						Console.Write("\b \b");
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
