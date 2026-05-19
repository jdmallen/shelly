using TextCopy;

namespace JDMallen.Shelly;

public static class ReplLoop
{
	public static async Task<ReplResult> RunAsync(
		IChatProvider provider,
		string? initialPrompt,
		CancellationToken cancellationToken = default)
	{
		string context = ShellContext.Build();
		string? prompt = initialPrompt;

		while (true)
		{
			if (string.IsNullOrWhiteSpace(prompt))
			{
				WriteColor("What do you want to do? ", ConsoleColor.Cyan);
				string? line = Console.ReadLine();
				if (string.IsNullOrWhiteSpace(line))
				{
					return ReplResult.Quit();
				}

				prompt = line;
			}

			WriteColor("Thinking...\n", ConsoleColor.Cyan);
			string suggestion;
			try
			{
				suggestion = await provider.GetSuggestionAsync(prompt, context, cancellationToken);
			}
			catch (Exception ex)
			{
				WriteColor($"Error: {ex.Message}\n", ConsoleColor.Red);

				return ReplResult.Quit();
			}

			if (string.IsNullOrWhiteSpace(suggestion))
			{
				WriteColor("No suggestion received from API\n", ConsoleColor.Red);

				return ReplResult.Quit();
			}

			Console.WriteLine();
			WriteColor("Suggestion:\n", ConsoleColor.Green);
			WriteColor($"{suggestion}\n", ConsoleColor.Yellow);
			Console.WriteLine();
			WriteOptions();

			ConsoleKey choice = Console.ReadKey(intercept: true).Key;
			Console.WriteLine();

			switch (choice)
			{
				case ConsoleKey.E:
					return ReplResult.Execute(suggestion);

				case ConsoleKey.C:
					await ClipboardService.SetTextAsync(suggestion, cancellationToken);
					WriteColor("Copied to clipboard\n", ConsoleColor.Green);

					return ReplResult.Quit();

				case ConsoleKey.R:
					WriteColor("What should be different? ", ConsoleColor.Cyan);
					string? refinement = Console.ReadLine();
					if (!string.IsNullOrWhiteSpace(refinement))
					{
						prompt = $"{prompt} (refinement: {refinement})";
					}

					continue;

				case ConsoleKey.Q:
				case ConsoleKey.Escape:
					Console.WriteLine("Bye");

					return ReplResult.Quit();

				default:
					WriteColor("Invalid choice\n", ConsoleColor.Yellow);

					continue;
			}
		}
	}

	private static void WriteOptions()
	{
		Console.Write("[");
		WriteColor("e", ConsoleColor.Green);
		Console.Write("]xecute  [");
		WriteColor("c", ConsoleColor.Green);
		Console.Write("]opy  [");
		WriteColor("r", ConsoleColor.Green);
		Console.Write("]etry  [");
		WriteColor("q", ConsoleColor.Green);
		Console.Write("]uit ");
	}

	private static void WriteColor(string text, ConsoleColor color)
	{
		ConsoleColor previous = Console.ForegroundColor;
		Console.ForegroundColor = color;
		Console.Write(text);
		Console.ForegroundColor = previous;
	}
}

public readonly record struct ReplResult(bool ShouldExecute, string? Command)
{
	public static ReplResult Execute(string command) => new(true, command);

	public static ReplResult Quit() => new(false, null);
}
