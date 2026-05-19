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

			(ActionOutcome outcome, string? refinement) = await ActOnSuggestionAsync(
				provider,
				suggestion,
				context,
				cancellationToken);

			switch (outcome)
			{
				case ActionOutcome.Execute:
					return ReplResult.Execute(suggestion);
				case ActionOutcome.Quit:
					return ReplResult.Quit();
				case ActionOutcome.Retry:
					if (!string.IsNullOrWhiteSpace(refinement))
					{
						prompt = $"{prompt} (refinement: {refinement})";
					}

					continue;
			}
		}
	}

	private static async Task<(ActionOutcome, string?)> ActOnSuggestionAsync(
		IChatProvider provider,
		string suggestion,
		string context,
		CancellationToken cancellationToken)
	{
		while (true)
		{
			Console.WriteLine();
			WriteColor("Suggestion:\n", ConsoleColor.Green);
			WriteColor($"{suggestion}\n", ConsoleColor.Yellow);
			Console.WriteLine();
			WriteOptions();

			ConsoleKey choice = Console.ReadKey(intercept: true).Key;
			Console.WriteLine();

			switch (choice)
			{
				case ConsoleKey.X:
					return (ActionOutcome.Execute, null);

				case ConsoleKey.E:
					WriteColor("Explaining...\n", ConsoleColor.Cyan);
					string explanation;
					try
					{
						explanation = await provider.ExplainCommandAsync(suggestion, context, cancellationToken);
					}
					catch (Exception ex)
					{
						WriteColor($"Error: {ex.Message}\n", ConsoleColor.Red);

						continue;
					}

					Console.WriteLine();
					WriteColor("Explanation:\n", ConsoleColor.Green);
					Console.WriteLine(explanation);

					continue;

				case ConsoleKey.C:
					await ClipboardService.SetTextAsync(suggestion, cancellationToken);
					WriteColor("Copied to clipboard\n", ConsoleColor.Green);

					return (ActionOutcome.Quit, null);

				case ConsoleKey.R:
					WriteColor("What should be different? ", ConsoleColor.Cyan);
					string? refinement = Console.ReadLine();

					return (ActionOutcome.Retry, refinement);

				case ConsoleKey.Q:
				case ConsoleKey.Escape:
					Console.WriteLine("Goodbye!");

					return (ActionOutcome.Quit, null);

				default:
					WriteColor("Invalid choice\n", ConsoleColor.Yellow);

					continue;
			}
		}
	}

	private static void WriteOptions()
	{
		Console.Write("e[");
		WriteColor("x", ConsoleColor.Green);
		Console.Write("]ecute  [");
		WriteColor("e", ConsoleColor.Green);
		Console.Write("]xplain  [");
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

	private enum ActionOutcome
	{
		Execute,
		Retry,
		Quit,
	}
}

public readonly record struct ReplResult(bool ShouldExecute, string? Command)
{
	public static ReplResult Execute(string command) => new(true, command);

	public static ReplResult Quit() => new(false, null);
}
