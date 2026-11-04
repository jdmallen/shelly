using JDMallen.Shelly.Tests.Fakes;

using Xunit;

namespace JDMallen.Shelly.Tests;

public class MainMenuTests
{
	private readonly FakeConsoleIO _io = new();

	[Theory]
	[InlineData(ConsoleKey.Enter, MainMenuChoice.Ask)]
	[InlineData(ConsoleKey.D1, MainMenuChoice.Ask)]
	[InlineData(ConsoleKey.D2, MainMenuChoice.Configure)]
	[InlineData(ConsoleKey.NumPad3, MainMenuChoice.Info)]
	[InlineData(ConsoleKey.D0, MainMenuChoice.Exit)]
	[InlineData(ConsoleKey.NumPad0, MainMenuChoice.Exit)]
	[InlineData(ConsoleKey.Escape, MainMenuChoice.Exit)]
	internal void Show_KeyPicksChoice(ConsoleKey key, MainMenuChoice expected)
	{
		_io.ReadKeys.Enqueue(key);

		Assert.Equal(expected, MainMenu.Show(_io, "shelly 9.9.9"));
	}

	[Fact]
	public void Show_RendersVersionAndNumberedOptionsWithAskAsDefault()
	{
		_io.ReadKeys.Enqueue(ConsoleKey.Enter);

		MainMenu.Show(_io, "shelly 9.9.9");

		string output = _io.Output.ToString().ReplaceLineEndings("\n");
		Assert.StartsWith("shelly 9.9.9\n", output);
		Assert.Contains("  1) Ask a question (default)\n", output);
		Assert.Contains("  2) Configure\n", output);
		Assert.Contains("  3) Info\n  0) Exit\n", output);
		Assert.DoesNotContain("4)", output);
		Assert.Contains("Choose (Enter = 1):", output);
	}

	[Fact]
	public void Show_MissingSettings_WarnsNextToConfigure()
	{
		_io.ReadKeys.Enqueue(ConsoleKey.Enter);

		MainMenu.Show(
			_io,
			"shelly 9.9.9",
			["azure.endpoint", "AZURE_OPENAI_API_KEY"]);

		Assert.Contains(
			"  2) Configure (needs setup: azure.endpoint, AZURE_OPENAI_API_KEY)\n",
			_io.Output.ToString().ReplaceLineEndings("\n"));
	}

	[Fact]
	public void Show_NothingMissing_NoWarning()
	{
		_io.ReadKeys.Enqueue(ConsoleKey.Enter);

		MainMenu.Show(_io, "shelly 9.9.9", []);

		Assert.DoesNotContain("needs setup", _io.Output.ToString());
	}

	[Fact]
	public void Show_InvalidKey_AsksAgain()
	{
		_io.ReadKeys.Enqueue(ConsoleKey.D4);
		_io.ReadKeys.Enqueue(ConsoleKey.Z);
		_io.ReadKeys.Enqueue(ConsoleKey.D2);

		Assert.Equal(
			MainMenuChoice.Configure,
			MainMenu.Show(_io, "shelly 9.9.9"));
		Assert.Contains(Strings.InvalidChoice, _io.Output.ToString());
	}
}
