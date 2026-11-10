namespace JDMallen.Shelly;

/// <summary>
/// The menu and wizard's input primitives. Choices are numbered rather than
/// lettered so translated labels need no hotkey markers.
/// </summary>
internal sealed class ConsolePrompts(IConsoleIO io)
{
	private const string INDENT = "  ";

	/// <summary>Returned by <see cref="Choose" /> when the user presses Esc.</summary>
	internal const int CANCELLED = -1;

	/// <summary>
	/// Lists <paramref name="options" /> numbered from 1 and reads one key.
	/// Enter picks <paramref name="defaultIndex" />.
	/// </summary>
	/// <param name="defaultIndex">The zero-based index of the default option.</param>
	/// <param name="warnings">
	/// Optional, by option index: text shown in yellow after that option.
	/// </param>
	/// <param name="zeroOption">
	/// Optional label listed last as <c>0)</c>, for leaving the menu (Exit,
	/// Back). Pressing 0 returns <see cref="CANCELLED" />, as Esc does.
	/// </param>
	/// <param name="options">The options to display.</param>
	/// <returns>The zero-based index chosen, or <see cref="CANCELLED" />.</returns>
	public int Choose(
		IReadOnlyList<string> options,
		int defaultIndex,
		IReadOnlyDictionary<int, string>? warnings = null,
		string? zeroOption = null)
	{
		// Single key presses, so at most nine options.
		ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Count, 9);

		for (var index = 0; index < options.Count; index++)
		{
			io.Write($"{INDENT}");
			io.Write($"{index + 1}", ConsoleColor.Green);
			io.Write($") {options[index]}");
			if (index == defaultIndex)
			{
				io.Write($" {Strings.DefaultMarker}");
			}

			if (warnings is not null
				&& warnings.TryGetValue(index, out string? warning))
			{
				io.Write($" ({warning})", ConsoleColor.Yellow);
			}

			io.WriteLine();
		}

		if (zeroOption is not null)
		{
			io.Write($"{INDENT}");
			io.Write("0", ConsoleColor.Green);
			io.WriteLine($") {zeroOption}");
		}

		io.WriteLine();
		while (true)
		{
			io.Write($"{Strings.Choose(defaultIndex + 1)} ", ConsoleColor.Cyan);
			ConsoleKey key = io.ReadKey();

			int chosen = key switch
			{
				ConsoleKey.Enter  => defaultIndex,
				ConsoleKey.Escape => CANCELLED,
				ConsoleKey.D0 or ConsoleKey.NumPad0 when zeroOption is not null
					=> CANCELLED,
				>= ConsoleKey.D1 and <= ConsoleKey.D9 => key - ConsoleKey.D1,
				>= ConsoleKey.NumPad1 and <= ConsoleKey.NumPad9 => key
					- ConsoleKey.NumPad1,
				_ => int.MinValue,
			};

			switch (chosen)
			{
				case CANCELLED:
					// Echo the 0 like any other number; Esc leaves the line blank.
					io.WriteLine(key == ConsoleKey.Escape ? string.Empty : "0");

					return CANCELLED;
				case >= 0 when chosen < options.Count:
					io.WriteLine($"{chosen + 1}");

					return chosen;
				default:
					io.WriteLine();
					io.Write($"{Strings.InvalidChoice}\n", ConsoleColor.Yellow);

					break;
			}
		}
	}

	/// <summary>
	/// Reads a line, offering <paramref name="current" /> as the value Enter
	/// keeps. <paramref name="validate" /> returns an error message, or null
	/// when the value is acceptable; an invalid value re-prompts.
	/// </summary>
	public string Text(
		string label,
		string current,
		Func<string, string?>? validate = null)
	{
		while (true)
		{
			WriteLabel(label, current);
			string? line = io.ReadLine();

			// End of input keeps the current value rather than looping forever.
			if (line is null)
			{
				return current;
			}

			string value = string.IsNullOrWhiteSpace(line)
				? current
				: Unquote(line);
			string? error = string.IsNullOrWhiteSpace(value)
				? Strings.ValueRequired
				: validate?.Invoke(value);

			if (error is null)
			{
				return value;
			}

			io.Write($"{INDENT}{error}\n", ConsoleColor.Yellow);
		}
	}

	/// <summary>
	/// Reads a value that may be left empty: Enter keeps
	/// <paramref name="current" />, a lone <c>-</c> clears it, and anything
	/// else must pass <paramref name="validate" />.
	/// </summary>
	public string OptionalText(
		string label,
		string current,
		Func<string, string?>? validate = null)
	{
		const string clear = "-";
		var labelWithHint = $"{label} ({Strings.OptionalTextHint})";
		while (true)
		{
			WriteLabel(labelWithHint, current);
			string? line = io.ReadLine();
			if (line is null || string.IsNullOrWhiteSpace(line))
			{
				return current;
			}

			string value = Unquote(line);
			if (value == clear)
			{
				return string.Empty;
			}

			string? error = validate?.Invoke(value);
			if (error is null)
			{
				return value;
			}

			io.Write($"{INDENT}{error}\n", ConsoleColor.Yellow);
		}
	}

	/// <summary>
	/// <paramref name="line" /> trimmed, without one pair of matching
	/// surrounding quotes, so a value pasted as <c>"abc"</c> or <c>'abc'</c>
	/// reads as <c>abc</c>.
	/// </summary>
	internal static string Unquote(string line)
	{
		string value = line.Trim();

		return value is ['"' or '\'', _, ..]
			&& value[^1] == value[0]
				? value[1..^1].Trim()
				: value;
	}

	/// <summary>Reads a whole number greater than zero.</summary>
	public int PositiveInteger(string label, int current)
	{
		string text = Text(
			label,
			current > 0 ? $"{current}" : string.Empty,
			value => int.TryParse(value, out int parsed) && parsed > 0
				? null
				: Strings.InvalidPositiveInteger);

		return int.TryParse(text, out int result) ? result : current;
	}

	/// <summary>Reads an absolute http:// or https:// URL.</summary>
	public string HttpUrl(string label, string current) =>
		Text(
			label,
			current,
			value => IsHttpUrl(value) ? null : Strings.InvalidHttpUrl);

	/// <summary>
	/// Reads a yes/no setting the way <see cref="Text" /> reads others:
	/// <c>label (y/n) [y]: </c>, where Enter keeps <paramref name="current" />.
	/// </summary>
	public bool Boolean(string label, bool current)
	{
		string text = Text(
			$"{label} (y/n)",
			current ? "y" : "n",
			value => ParseYesNo(value) is null ? Strings.InvalidChoice : null);

		return ParseYesNo(text) ?? current;
	}

	private static bool? ParseYesNo(string value) =>
		value.ToLowerInvariant() switch
		{
			"y" or "yes" => true,
			"n" or "no"  => false,
			_            => null,
		};

	/// <summary>
	/// Asks a yes/no question answered with Y or N; Enter takes
	/// <paramref name="defaultYes" /> and Esc means no.
	/// </summary>
	public bool YesNo(string question, bool defaultYes, bool indent = false)
	{
		string hint = defaultYes ? "[Y/n]" : "[y/N]";
		while (true)
		{
			io.Write(
				$"{(indent ? INDENT : string.Empty)}{question} {hint} ",
				ConsoleColor.Cyan);
			ConsoleKey key = io.ReadKey();

			bool? answer = key switch
			{
				ConsoleKey.Y      => true,
				ConsoleKey.N      => false,
				ConsoleKey.Enter  => defaultYes,
				ConsoleKey.Escape => false,
				_                 => null,
			};

			if (answer is not null)
			{
				io.WriteLine(answer.Value ? "y" : "n");

				return answer.Value;
			}

			io.WriteLine();
			io.Write($"{Strings.InvalidChoice}\n", ConsoleColor.Yellow);
		}
	}

	/// <summary>Reads a secret, echoing <c>*</c>; null or empty means skip.</summary>
	public string? Secret(string label)
	{
		io.Write($"{INDENT}{label} ", ConsoleColor.Cyan);

		return io.ReadSecret();
	}

	private static bool IsHttpUrl(string value) =>
		Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
		&& (uri.Scheme == Uri.UriSchemeHttp
			|| uri.Scheme == Uri.UriSchemeHttps);

	private void WriteLabel(string label, string current)
	{
		io.Write($"{INDENT}{label}", ConsoleColor.Cyan);
		if (!string.IsNullOrEmpty(current))
		{
			io.Write($" [{current}]");
		}

		io.Write(": ");
	}
}
