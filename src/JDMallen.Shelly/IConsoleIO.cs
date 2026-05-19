namespace JDMallen.Shelly;

internal interface IConsoleIO
{
	void Write(string text, ConsoleColor? color = null);

	void WriteLine(string text = "");

	string? ReadLine();

	ConsoleKey ReadKey();
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

	public ConsoleKey ReadKey() => Console.ReadKey(intercept: true).Key;
}
