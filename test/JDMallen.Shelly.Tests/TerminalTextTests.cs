using Xunit;

namespace JDMallen.Shelly.Tests;

public class TerminalTextTests
{
	[Theory]
	[InlineData("ls -la", "ls -la")]
	[InlineData("line one\nline two", "line one\nline two")]
	[InlineData("a\tb", "a\tb")]
	[InlineData("line one\r\nline two", "line one\nline two")]
	[InlineData("ls\rrm", @"ls\rrm")]
	[InlineData("\u001b[2K", @"\x1b[2K")]
	[InlineData("bell\u0007", @"bell\x07")]
	[InlineData("del\u007f", @"del\x7f")]
	[InlineData("csi\u009b2K", @"csi\x9b2K")]
	[InlineData("trailing\r", @"trailing\r")]
	public void Escape_ShowsControlCharacters(string input, string expected)
		=> Assert.Equal(expected, TerminalText.Escape(input));

	[Theory]
	[InlineData("ls -la", false)]
	[InlineData("ls\n\tcat file", false)]
	[InlineData("ls\r\ncat file", false)]
	[InlineData("ls\rrm", true)]
	[InlineData("trailing\r", true)]
	[InlineData("\u001b[2K", true)]
	[InlineData("csi\u009b2K", true)]
	public void HasHiddenCharacters_MatchesWhatEscapeChanges(
		string input,
		bool expected)
		=> Assert.Equal(expected, TerminalText.HasHiddenCharacters(input));

	[Fact]
	public void Escape_PlainText_ReturnsTheSameInstance()
	{
		const string TEXT = "ls -la";

		Assert.Same(TEXT, TerminalText.Escape(TEXT));
	}
}
