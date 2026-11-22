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
	[InlineData("\e[2K", @"\x1b[2K")]
	[InlineData("bell\a", @"bell\x07")]
	[InlineData("del\u007f", @"del\x7f")]
	[InlineData("csi\u009b2K", @"csi\x9b2K")]
	[InlineData("trailing\r", @"trailing\r")]
	[InlineData("rm -rf ~ #\u202e/ pmt/", @"rm -rf ~ #\u202e/ pmt/")]
	[InlineData("a\u2066b\u2069", @"a\u2066b\u2069")]
	[InlineData("l\u200bs", @"l\u200bs")]
	[InlineData("\ufeffls", @"\ufeffls")]
	[InlineData("café ñ 日本語", "café ñ 日本語")]
	public void Escape_ShowsControlCharacters(string input, string expected)
		=> Assert.Equal(expected, TerminalText.Escape(input));

	[Theory]
	[InlineData("ls -la", false)]
	[InlineData("ls\n\tcat file", false)]
	[InlineData("ls\r\ncat file", false)]
	[InlineData("ls\rrm", true)]
	[InlineData("trailing\r", true)]
	[InlineData("\e[2K", true)]
	[InlineData("csi\u009b2K", true)]
	[InlineData("ls #\u202e", true)]
	[InlineData("l\u200ds", true)]
	[InlineData("café 日本語", false)]
	[InlineData("soft\u00adhyphen", false)]
	public void HasHiddenCharacters_MatchesWhatEscapeChanges(
		string input,
		bool expected)
		=> Assert.Equal(expected, TerminalText.HasHiddenCharacters(input));

	[Fact]
	public void Escape_PlainText_ReturnsTheSameInstance()
	{
		const string text = "ls -la";

		Assert.Same(text, TerminalText.Escape(text));
	}
}
