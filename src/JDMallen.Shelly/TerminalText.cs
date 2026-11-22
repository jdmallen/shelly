using System.Globalization;
using System.Text;

namespace JDMallen.Shelly;

/// <summary>
/// Makes text from a model or a server safe to print: a raw carriage return
/// or escape sequence could hide part of a command, and a bidi override or
/// zero-width character could reorder or hide it ("Trojan Source"), so
/// what's shown would no longer be what runs.
/// </summary>
internal static class TerminalText
{
	/// <summary>
	/// Shows every control character other than newline and tab as an
	/// escape, e.g. <c>\x1b</c> for ESC and <c>\r</c> for a lone carriage
	/// return, and every bidi control or zero-width character as
	/// <c>\uXXXX</c>, e.g. <c>\u202e</c>. A CRLF line ending stays a plain
	/// line break.
	/// </summary>
	public static string Escape(string text)
	{
		if (!text.Any(NeedsEscape))
		{
			return text;
		}

		var escaped = new StringBuilder(text.Length + 16);
		for (var index = 0; index < text.Length; index++)
		{
			char character = text[index];
			if (character == '\r'
				&& index + 1 < text.Length
				&& text[index + 1] == '\n')
			{
				// CRLF: drop the CR, the LF is appended next.
				continue;
			}

			if (!NeedsEscape(character))
			{
				escaped.Append(character);
			}
			else
				switch (character)
				{
					case '\r':
						escaped.Append(@"\r");

						break;
					case <= '\u00ff':
						escaped.Append(@"\x")
							.Append(
								((int)character).ToString(
									"x2",
									CultureInfo.InvariantCulture));

						break;
					default:
						escaped.Append(@"\u")
							.Append(
								((int)character).ToString(
									"x4",
									CultureInfo.InvariantCulture));

						break;
				}
		}

		return escaped.ToString();
	}

	/// <summary>
	/// Whether <see cref="Escape" /> would show anything escaped: a control
	/// character other than newline, tab, or the CR of a CRLF, or an
	/// invisible formatting character.
	/// </summary>
	public static bool HasHiddenCharacters(string text)
	{
		for (var index = 0; index < text.Length; index++)
		{
			char character = text[index];
			bool isCrlf = character == '\r'
				&& index + 1 < text.Length
				&& text[index + 1] == '\n';
			if (NeedsEscape(character) && !isCrlf)
			{
				return true;
			}
		}

		return false;
	}

	private static bool NeedsEscape(char character) =>
		(char.IsControl(character) && character is not ('\n' or '\t'))
		|| IsInvisibleFormatting(character);

	// Bidi embeddings, overrides and isolates, the directional marks, and
	// the zero-width characters (ZWSP, ZWNJ, ZWJ, word joiner, BOM). ZWNJ
	// and ZWJ also occur in Persian text and emoji sequences, which then
	// show escaped in an explanation; that's the cost of catching them in a
	// command. Other format characters, such as the soft hyphen, are left
	// alone.
	private static bool IsInvisibleFormatting(char character) =>
		character is >= '\u202a' and <= '\u202e'
			or >= '\u2066' and <= '\u2069'
			or >= '\u200b' and <= '\u200f'
			or '\u061c'
			or '\u2060'
			or '\ufeff';
}
