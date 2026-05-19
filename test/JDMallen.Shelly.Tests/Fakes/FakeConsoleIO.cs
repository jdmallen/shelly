using System.Text;
using JDMallen.Shelly;

namespace JDMallen.Shelly.Tests.Fakes;

internal sealed class FakeConsoleIO : IConsoleIO
{
	public Queue<string?> ReadLines { get; } = new();

	public Queue<ConsoleKey> ReadKeys { get; } = new();

	public StringBuilder Output { get; } = new();

	public void Write(string text, ConsoleColor? color = null) => Output.Append(text);

	public void WriteLine(string text = "") => Output.AppendLine(text);

	public string? ReadLine() => ReadLines.Count == 0 ? null : ReadLines.Dequeue();

	public ConsoleKey ReadKey() => ReadKeys.Dequeue();
}
