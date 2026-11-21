using System.Text;

namespace JDMallen.Shelly.Tests.Fakes;

internal sealed class FakeConsoleIO : IConsoleIO
{
	public bool IsInteractive { get; set; } = true;

	public bool IsInputRedirected { get; set; }

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

	public ConsoleKey ReadKey() => ReadKeys.Dequeue();

	public string? ReadSecret()
		=> ReadSecrets.Count == 0 ? null : ReadSecrets.Dequeue();
}
