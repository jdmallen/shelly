using System.Globalization;
using System.Text;

namespace JDMallen.Shelly;

/// <summary>
/// Makes text from a model or a server safe to print: a raw carriage return
/// or escape sequence could hide part of a command, so what's shown would no
/// longer be what runs.
/// </summary>
internal static class TerminalText
{
	/// <summary>
	/// Shows every control character other than newline and tab as an
	/// escape, e.g. <c>\x1b</c> for ESC and <c>\r</c> for a lone carriage
	/// return. A CRLF line ending stays a plain line break.
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
			else if (character == '\r')
			{
				escaped.Append(@"\r");
			}
			else
			{
				escaped.Append(@"\x").Append(
					((int)character).ToString(
						"x2",
						CultureInfo.InvariantCulture));
			}
		}

		return escaped.ToString();
	}

	private static bool NeedsEscape(char character) =>
		char.IsControl(character) && character is not ('\n' or '\t');
}
