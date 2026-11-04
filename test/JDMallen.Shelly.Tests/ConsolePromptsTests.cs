using JDMallen.Shelly.Tests.Fakes;

using Xunit;

namespace JDMallen.Shelly.Tests;

public class ConsolePromptsTests
{
	private readonly FakeConsoleIO _io = new();

	private ConsolePrompts Prompts => new(_io);

	[Fact]
	public void Text_Enter_KeepsCurrentValue()
	{
		_io.ReadLines.Enqueue("");

		Assert.Equal("claude", Prompts.Text("anthropic.model", "claude"));
		Assert.Contains("anthropic.model [claude]: ", _io.Output.ToString());
	}

	[Fact]
	public void Text_TrimsTypedValue()
	{
		_io.ReadLines.Enqueue("  qwen  ");

		Assert.Equal("qwen", Prompts.Text("openai.model", ""));
	}

	[Fact]
	public void Text_EmptyWithNoCurrentValue_AsksAgain()
	{
		_io.ReadLines.Enqueue("");
		_io.ReadLines.Enqueue("qwen");

		Assert.Equal("qwen", Prompts.Text("openai.model", ""));
		Assert.Contains(Strings.ValueRequired, _io.Output.ToString());
	}

	[Fact]
	public void Text_EndOfInput_KeepsCurrentValue()
		=> Assert.Equal("", Prompts.Text("openai.model", ""));

	[Theory]
	[InlineData("0")]
	[InlineData("-5")]
	[InlineData("lots")]
	public void PositiveInteger_RejectsInvalidThenAccepts(string invalid)
	{
		_io.ReadLines.Enqueue(invalid);
		_io.ReadLines.Enqueue("2048");

		Assert.Equal(2048, Prompts.PositiveInteger("openai.maxTokens", 500));
		Assert.Contains(Strings.InvalidPositiveInteger, _io.Output.ToString());
	}

	[Theory]
	[InlineData("localhost:8080/v1")]
	[InlineData("ftp://example.com")]
	[InlineData("/v1")]
	public void HttpUrl_RejectsNonHttpUrls(string invalid)
	{
		_io.ReadLines.Enqueue(invalid);
		_io.ReadLines.Enqueue("http://localhost:8080/v1");

		Assert.Equal(
			"http://localhost:8080/v1",
			Prompts.HttpUrl("openai.baseUrl", ""));
		Assert.Contains(Strings.InvalidHttpUrl, _io.Output.ToString());
	}

	[Theory]
	[InlineData(ConsoleKey.Y, false, true)]
	[InlineData(ConsoleKey.N, true, false)]
	[InlineData(ConsoleKey.Enter, true, true)]
	[InlineData(ConsoleKey.Enter, false, false)]
	[InlineData(ConsoleKey.Escape, true, false)]
	public void YesNo_KeyAndDefault(
		ConsoleKey key,
		bool defaultYes,
		bool expected)
	{
		_io.ReadKeys.Enqueue(key);

		Assert.Equal(expected, Prompts.YesNo("Save changes?", defaultYes));
		Assert.Contains(defaultYes ? "[Y/n]" : "[y/N]", _io.Output.ToString());
	}

	[Fact]
	public void Choose_RejectsNumberOutOfRange()
	{
		_io.ReadKeys.Enqueue(ConsoleKey.D4);
		_io.ReadKeys.Enqueue(ConsoleKey.D3);

		Assert.Equal(2, Prompts.Choose(["a", "b", "c"], 1));
		Assert.Contains("  2) b (default)", _io.Output.ToString());
		Assert.Contains(Strings.InvalidChoice, _io.Output.ToString());
	}

	[Fact]
	public void Choose_ZeroOption_ListedLastAndCancels()
	{
		_io.ReadKeys.Enqueue(ConsoleKey.D0);

		Assert.Equal(
			ConsolePrompts.CANCELLED,
			Prompts.Choose(["a", "b"], 0, zeroOption: "Back"));
		Assert.Contains(
			"  2) b\n  0) Back\n",
			_io.Output.ToString().ReplaceLineEndings("\n"));
	}

	[Fact]
	public void Choose_WithoutZeroOption_ZeroIsInvalid()
	{
		_io.ReadKeys.Enqueue(ConsoleKey.D0);
		_io.ReadKeys.Enqueue(ConsoleKey.D1);

		Assert.Equal(0, Prompts.Choose(["a", "b"], 1));
		Assert.Contains(Strings.InvalidChoice, _io.Output.ToString());
	}

	[Theory]
	[InlineData("", true, true)]
	[InlineData("", false, false)]
	[InlineData("N", true, false)]
	[InlineData("yes", false, true)]
	public void Boolean_EnterKeepsCurrent(
		string typed,
		bool current,
		bool expected)
	{
		_io.ReadLines.Enqueue(typed);

		Assert.Equal(expected, Prompts.Boolean("flag", current));
		Assert.Contains(
			$"  flag (y/n) [{(current ? "y" : "n")}]: ",
			_io.Output.ToString());
	}
}
