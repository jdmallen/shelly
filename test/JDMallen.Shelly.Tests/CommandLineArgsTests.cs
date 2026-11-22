using Xunit;

namespace JDMallen.Shelly.Tests;

public class CommandLineArgsTests
{
	[Fact]
	public void Parse_NoArgs_RunsWithNullPrompt() => Assert.Equal(
		new CommandLineArgs.Run(null),
		CommandLineArgs.Parse([]));

	[Fact]
	public void Parse_PositionalWords_JoinedIntoPrompt() => Assert.Equal(
		new CommandLineArgs.Run("list files"),
		CommandLineArgs.Parse(["list", "files"]));

	[Theory]
	[InlineData("--help")]
	[InlineData("-h")]
	[InlineData("-?")]
	public void Parse_HelpFlags_ShowHelp(string flag)
		=> Assert.IsType<CommandLineArgs.ShowHelp>(
			CommandLineArgs.Parse([flag]));

	[Fact]
	public void Parse_VersionFlag_ShowVersion()
		=> Assert.IsType<CommandLineArgs.ShowVersion>(
			CommandLineArgs.Parse(["--version"]));

	[Fact]
	public void Parse_ConfigFlag_ShowConfig()
		=> Assert.IsType<CommandLineArgs.ShowConfig>(
			CommandLineArgs.Parse(["--config"]));

	[Fact]
	public void Parse_InfoFlag_ShowInfo()
		=> Assert.IsType<CommandLineArgs.ShowInfo>(
			CommandLineArgs.Parse(["--info"]));

	[Fact]
	public void Parse_UnknownOption_ReturnsError()
	{
		var error = Assert.IsType<CommandLineArgs.Error>(
			CommandLineArgs.Parse(["--bogus"]));

		Assert.Contains("Option 'bogus' is unknown.", error.Message);
	}

	[Fact]
	public void Parse_OptionTerminator_TreatsRestAsPrompt() => Assert.Equal(
		new CommandLineArgs.Run("-la files"),
		CommandLineArgs.Parse(["--", "-la", "files"]));

	[Fact]
	public void Parse_OptionTerminatorAlone_RunsWithNullPrompt()
		=> Assert.Equal(
			new CommandLineArgs.Run(null),
			CommandLineArgs.Parse(["--"]));

	[Fact]
	public void Parse_HelpAfterPromptWords_IsPromptText() => Assert.Equal(
		new CommandLineArgs.Run("list files --help"),
		CommandLineArgs.Parse(["list", "files", "--help"]));

	[Fact]
	public void Parse_UnknownOptionAfterPromptWords_IsPromptText()
		=> Assert.Equal(
			new CommandLineArgs.Run("find files with grep -r"),
			CommandLineArgs.Parse(["find", "files", "with", "grep", "-r"]));

	[Fact]
	public void Parse_TerminatorAfterPromptWords_IsPromptText() => Assert.Equal(
		new CommandLineArgs.Run("list -- files"),
		CommandLineArgs.Parse(["list", "--", "files"]));

	[Fact]
	public void Parse_TerminatorLetsThePromptLeadWithADash() => Assert.Equal(
		new CommandLineArgs.Run("-la but also sort by filename for ls"),
		CommandLineArgs.Parse(
		[
			"--", "-la", "but", "also", "sort", "by", "filename", "for", "ls",
		]));

	[Fact]
	public void Parse_OptionBeforePromptWords_StillWorks()
		=> Assert.IsType<CommandLineArgs.ShowInfo>(
			CommandLineArgs.Parse(["--info", "list", "files"]));

	[Fact]
	public void Parse_HelpAfterTerminator_IsPromptText() => Assert.Equal(
		new CommandLineArgs.Run("--help"),
		CommandLineArgs.Parse(["--", "--help"]));
}
