using System.Globalization;

namespace JDMallen.Shelly;

/// <summary>
/// System prompts. They stay in English, which models follow most reliably;
/// for a translated UI culture, <see cref="Explain" /> asks for the
/// explanation in that language. Suggestions are commands, so they need no
/// language instruction.
/// </summary>
internal static class Prompts
{
	public static string Suggestion(string context) =>
		$"""
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

		 Context: {context}
		 """;

	public static string Explain(string context, CultureInfo? culture = null)
	{
		string responseLanguage = Strings.ResponseLanguage(culture);
		string languageInstruction = responseLanguage.Length == 0
			? string.Empty
			: $" Write your explanation in {responseLanguage}.";

		return $"""
		        You are a shell command expert. The user will give you a shell command. Explain in 1-3 short sentences what it does, including what each pipe stage or flag contributes. Plain text only — no markdown, no code fences, no bullet lists. Be concise.{languageInstruction}

		        Context: {context}
		        """;
	}
}
