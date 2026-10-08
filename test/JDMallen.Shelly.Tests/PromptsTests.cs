using System.Globalization;
using Xunit;

namespace JDMallen.Shelly.Tests;

public class PromptsTests
{
	private const string ENGLISH_EXPLAIN = """
	                                       You are a shell command expert. The user will give you a shell command. Explain in 1-3 short sentences what it does, including what each pipe stage or flag contributes. Plain text only — no markdown, no code fences, no bullet lists. Be concise.

	                                       Context: ctx
	                                       """;

	[Fact]
	public void Suggestion_MatchesOriginalEnglishPrompt()
	{
		string expected = """
		                  You are a shell command expert. Given a description of what the user wants to do, output ONLY the shell command(s) that accomplish it. No explanations, no markdown, no code fences, no backticks, no language tags — just the raw command(s). If multiple commands are needed, separate them with && or use appropriate shell syntax.

		                  EXAMPLES:
		                  Bad:
		                  ```bash
		                  ls -la
		                  ```
		                  Good:
		                  ls -la

		                  Bad: `find . -name '*.cs'`
		                  Good: find . -name '*.cs'

		                  Context: ctx
		                  """;

		Assert.Equal(expected, Prompts.Suggestion("ctx"));
	}

	[Theory]
	[InlineData("")]
	[InlineData("en-US")]
	[InlineData("en-GB")]
	[InlineData("ar-SA")]
	[InlineData("pt-PT")]
	public void Explain_UntranslatedCulture_HasNoLanguageInstruction(string cultureName)
	{
		Assert.Equal(
			ENGLISH_EXPLAIN,
			Prompts.Explain("ctx", CultureInfo.GetCultureInfo(cultureName)));
	}

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
			"Be concise.",
			$"Be concise. Write your explanation in {languageName}.");

		Assert.Equal(
			expected,
			Prompts.Explain("ctx", CultureInfo.GetCultureInfo(cultureName)));
	}

	[Fact]
	public void Explain_ContextWithBraces_IsInsertedVerbatim()
	{
		Assert.EndsWith(
			"Context: echo {a,b}",
			Prompts.Explain("echo {a,b}", CultureInfo.GetCultureInfo("ja")));
	}
}
