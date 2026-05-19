namespace JDMallen.Shelly;

internal static class CommandSanitizer
{
	public static string Strip(string raw)
	{
		string s = raw.Trim();

		if (s.StartsWith("```", StringComparison.Ordinal))
		{
			int firstNewline = s.IndexOf('\n');
			s = firstNewline >= 0 ? s[(firstNewline + 1)..] : s[3..];

			if (s.EndsWith("```", StringComparison.Ordinal))
			{
				s = s[..^3];
			}

			s = s.Trim();
		}

		if (s.Length >= 2 && s.StartsWith('`') && s.EndsWith('`'))
		{
			s = s[1..^1].Trim();
		}

		return s;
	}
}
