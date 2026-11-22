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

			// Cut at the closing fence: anything after it is prose.
			int closingFence = s.IndexOf("```", StringComparison.Ordinal);
			if (closingFence >= 0)
			{
				s = s[..closingFence];
			}

			s = s.Trim();
		}

		// One pair wrapping the whole command, not the outer ends of two
		// substitutions (`which a` b `which c`, csh's only syntax for them).
		if (s.Length >= 2
			&& s.StartsWith('`')
			&& s.EndsWith('`')
			&& !s[1..^1].Contains('`'))
		{
			s = s[1..^1].Trim();
		}

		return s;
	}
}
