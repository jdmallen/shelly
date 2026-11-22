using System.Text;

namespace JDMallen.Shelly.Tests.Fakes;

internal sealed class FakeConsoleIO : IConsoleIO
{
	public bool IsInteractive { get; set; } = true;

	public bool IsInputRedirected { get; set; }

	public bool IsOutputRedirected { get; set; }

	/// <summary>The rest of stdin, for <see cref="ReadToEnd" />.</summary>
	public string StdinRemainder { get; set; } = string.Empty;

	public Queue<string?> ReadLines { get; } = new();

	public Queue<ConsoleKey> ReadKeys { get; } = new();

	public Queue<string?> ReadSecrets { get; } = new();

	public StringBuilder Output { get; } = new();

	public void Write(string text, ConsoleColor? color = null)
		=> Output.Append(text);

	public StringBuilder Errors { get; } = new();

	public void WriteLine(string text = "", ConsoleColor? color = null)
		=> Output.AppendLine(text);

	public void WriteError(string text) => Errors.AppendLine(text);

	public void WriteErrorInline(string text) => Errors.Append(text);

	public string? ReadLine()
		=> ReadLines.Count == 0 ? null : ReadLines.Dequeue();

	public string ReadToEnd() => StdinRemainder;

	/// <summary>
	/// The terminal's answer to <see cref="ConfirmOnTerminal" />; null means
	/// there is no terminal.
	/// </summary>
	public bool? TerminalAnswer { get; set; }

	public List<string> TerminalMessages { get; } = [];

	public bool? ConfirmOnTerminal(string message, string question)
	{
		TerminalMessages.Add(message);

		return TerminalAnswer;
	}

	/// <summary>
	/// Runs before each <see cref="ReadKey" />, e.g. a Ctrl+C.
	/// </summary>
	public Action? BeforeReadKey { get; set; }

	public ConsoleKey ReadKey()
	{
		BeforeReadKey?.Invoke();

		return ReadKeys.Dequeue();
	}

	public string? ReadSecret()
		=> ReadSecrets.Count == 0 ? null : ReadSecrets.Dequeue();
}
