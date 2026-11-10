using System.Globalization;

using Xunit;

namespace JDMallen.Shelly.Tests;

public class PromptsTests
{
	private const string CONTEXT_LINE = "Shell: Bash, OS: os, PWD: /pwd";

	private const string ENGLISH_EXPLAIN = """
										   You are a shell command expert. The user will give you a shell command. Explain in 1-3 short sentences what it does, including what each pipe stage or flag contributes. Plain text only — no markdown, no code fences, no bullet lists. Be concise. The command is for Bash; explain it in terms of that shell's syntax.

										   Context: Shell: Bash, OS: os, PWD: /pwd
										   """;

	private static ShellContext ContextFor(
		string shellName,
		string workingDirectory = "/pwd") =>
		new(Shell.FromNameOrPath(shellName), "os", workingDirectory);

	[Fact]
	public void Suggestion_Bash_MatchesEnglishPrompt()
	{
		const string expected = """
								You are a shell command expert. Given a description of what the user wants to do, output ONLY the shell command(s) that accomplish it. No explanations, no markdown, no code fences, no backticks, no language tags — just the raw command(s). If multiple commands are needed, separate them with && or use appropriate shell syntax.

								The user's shell is Bash. Write the command(s) for Bash specifically, using its syntax and builtins, so they run correctly when typed into it.

								EXAMPLES:
								Bad:
								```bash
								ls -la
								```
								Good:
								ls -la

								Bad: `find . -name '*.cs'`
								Good: find . -name '*.cs'

								Context: Shell: Bash, OS: os, PWD: /pwd
								""";

		Assert.Equal(expected, Prompts.Suggestion(ContextFor("bash")));
	}

	[Theory]
	[InlineData("fish", "Use fish syntax")]
	[InlineData("dash", "Use only POSIX sh features")]
	[InlineData("pwsh", "Use PowerShell cmdlets")]
	[InlineData("powershell", "`&&` and `||` do not exist")]
	[InlineData("cmd", "Use cmd.exe batch syntax")]
	[InlineData("nu", "Use Nushell syntax")]
	[InlineData("tcsh", "Use csh/tcsh syntax")]
	public void Suggestion_ShellWithDistinctSyntax_IncludesItsHint(
		string shellName,
		string hint)
	{
		string prompt = Prompts.Suggestion(ContextFor(shellName));

		Assert.Contains(hint, prompt);
	}

	[Theory]
	[InlineData("dash", "POSIX sh (dash)")]
	[InlineData("pwsh", "PowerShell")]
	[InlineData("xonsh", "xonsh")]
	public void Suggestion_NamesTheShell(string shellName, string displayName)
	{
		string prompt = Prompts.Suggestion(ContextFor(shellName));

		Assert.Contains($"The user's shell is {displayName}.", prompt);
		Assert.Contains($"Context: Shell: {displayName}, OS: os", prompt);
	}

	[Theory]
	[InlineData("bash")]
	[InlineData("zsh")]
	[InlineData("xonsh")]
	public void Suggestion_ShellWithoutHint_HasOnlyTheShellSentence(
		string shellName)
	{
		string prompt = Prompts.Suggestion(ContextFor(shellName));

		Assert.Contains(
			"so they run correctly when typed into it.\n\nEXAMPLES:",
			prompt);
	}

	[Theory]
	[InlineData("")]
	[InlineData("en-US")]
	[InlineData("en-GB")]
	[InlineData("ar-SA")]
	[InlineData("pt-PT")]
	public void Explain_UntranslatedCulture_HasNoLanguageInstruction(
		string cultureName) => Assert.Equal(
		ENGLISH_EXPLAIN,
		Prompts.Explain(
			ContextFor("bash"),
			CultureInfo.GetCultureInfo(cultureName)));

	[Theory]
	[InlineData("de", "German")]
	[InlineData("ja-JP", "Japanese")]
	[InlineData("pt-BR", "Brazilian Portuguese")]
	[InlineData("zh-CN", "Simplified Chinese")]
	[InlineData("zh-TW", "Traditional Chinese")]
	public void Explain_TranslatedCulture_AsksForThatLanguage(
		string cultureName,
		string languageName)
	{
		string expected = ENGLISH_EXPLAIN.Replace(
			"that shell's syntax.",
			$"that shell's syntax. Write your explanation in {languageName}.");

		Assert.Equal(
			expected,
			Prompts.Explain(
				ContextFor("bash"),
				CultureInfo.GetCultureInfo(cultureName)));
	}

	[Fact]
	public void Explain_NamesTheShell() => Assert.Contains(
		"The command is for fish;",
		Prompts.Explain(ContextFor("fish"), CultureInfo.InvariantCulture));

	[Fact]
	public void Explain_ContextWithBraces_IsInsertedVerbatim()
		=> Assert.EndsWith(
			"PWD: /tmp/{a,b}",
			Prompts.Explain(
				ContextFor("bash", "/tmp/{a,b}"),
				CultureInfo.GetCultureInfo("ja")));

	[Fact]
	public void ContextLine_DescribesShellOsAndDirectory() => Assert.Equal(
		CONTEXT_LINE,
		ContextFor("bash").Describe());
}
