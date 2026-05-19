using JDMallen.Shelly;
using Xunit;

namespace JDMallen.Shelly.Tests;

public class CommandSanitizerTests
{
	[Theory]
	[InlineData("ls -la", "ls -la")]
	[InlineData("  ls -la  ", "ls -la")]
	[InlineData("\nls -la\n", "ls -la")]
	public void Strip_TrimsWhitespace(string input, string expected)
	{
		Assert.Equal(expected, CommandSanitizer.Strip(input));
	}

	[Theory]
	[InlineData("```bash\nls -la\n```", "ls -la")]
	[InlineData("```sh\nfind . -name '*.cs'\n```", "find . -name '*.cs'")]
	[InlineData("```\nls -la\n```", "ls -la")]
	[InlineData("```bash\nls\ncat file\n```", "ls\ncat file")]
	[InlineData("  ```bash\nls\n```  ", "ls")]
	public void Strip_RemovesTripleBacktickFences(string input, string expected)
	{
		Assert.Equal(expected, CommandSanitizer.Strip(input));
	}

	[Theory]
	[InlineData("`ls -la`", "ls -la")]
	[InlineData("`ls`", "ls")]
	[InlineData("  `ls -la`  ", "ls -la")]
	public void Strip_RemovesSingleBacktickWrap(string input, string expected)
	{
		Assert.Equal(expected, CommandSanitizer.Strip(input));
	}

	[Theory]
	[InlineData("echo `date`", "echo `date`")]
	[InlineData("ls | grep `whoami`", "ls | grep `whoami`")]
	public void Strip_PreservesMidStringBackticks(string input, string expected)
	{
		Assert.Equal(expected, CommandSanitizer.Strip(input));
	}

	[Fact]
	public void Strip_EmptyInput_ReturnsEmpty()
	{
		Assert.Equal(string.Empty, CommandSanitizer.Strip(string.Empty));
	}

	[Fact]
	public void Strip_EmptyFencedBlock_ReturnsEmpty()
	{
		Assert.Equal(string.Empty, CommandSanitizer.Strip("```bash\n```"));
	}
}
