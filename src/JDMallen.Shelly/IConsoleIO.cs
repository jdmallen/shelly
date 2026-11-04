using System.Text;

namespace JDMallen.Shelly;

internal interface IConsoleIO
{
	void Write(string text, ConsoleColor? color = null);

	void WriteLine(string text = "");

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

	public void WriteLine(string text = "") => Console.WriteLine(text);

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
