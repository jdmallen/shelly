namespace JDMallen.Shelly;

public sealed class ReplLoop
{
	private readonly IChatProvider _provider;
	private readonly IConsoleIO _io;
	private readonly IClipboard _clipboard;

	internal ReplLoop(IChatProvider provider, IConsoleIO io, IClipboard clipboard)
	{
		_provider = provider;
		_io = io;
		_clipboard = clipboard;
	}

	public ReplLoop(IChatProvider provider)
		: this(provider, new SystemConsoleIO(), new TextCopyClipboard())
	{
	}

	public async Task<ReplResult> RunAsync(string? initialPrompt, CancellationToken cancellationToken = default)
	{
		string context = ShellContext.Build();
		string? prompt = initialPrompt;

		while (true)
		{
			if (string.IsNullOrWhiteSpace(prompt))
			{
				_io.Write("What do you want to do? ", ConsoleColor.Cyan);
				string? line = _io.ReadLine();
				if (string.IsNullOrWhiteSpace(line))
				{
					return ReplResult.Quit();
				}

				prompt = line;
			}

			_io.Write("Thinking...\n", ConsoleColor.Cyan);
			string suggestion;
			try
			{
				suggestion = await _provider.GetSuggestionAsync(prompt, context, cancellationToken);
			}
			catch (Exception ex)
			{
				_io.Write($"Error: {ex.Message}\n", ConsoleColor.Red);

				return ReplResult.Quit();
			}

			if (string.IsNullOrWhiteSpace(suggestion))
			{
				_io.Write("No suggestion received from API\n", ConsoleColor.Red);

				return ReplResult.Quit();
			}

			(ActionOutcome outcome, string? refinement) = await ActOnSuggestionAsync(
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

	private async Task<(ActionOutcome, string?)> ActOnSuggestionAsync(
		string suggestion,
		string context,
		CancellationToken cancellationToken)
	{
		while (true)
		{
			_io.WriteLine();
			_io.Write("Suggestion:\n", ConsoleColor.Green);
			_io.Write($"{suggestion}\n", ConsoleColor.Yellow);
			_io.WriteLine();
			WriteOptions();

			ConsoleKey choice = _io.ReadKey();
			_io.WriteLine();

			switch (choice)
			{
				case ConsoleKey.X:
					return (ActionOutcome.Execute, null);

				case ConsoleKey.E:
					_io.Write("Explaining...\n", ConsoleColor.Cyan);
					string explanation;
					try
					{
						explanation = await _provider.ExplainCommandAsync(suggestion, context, cancellationToken);
					}
					catch (Exception ex)
					{
						_io.Write($"Error: {ex.Message}\n", ConsoleColor.Red);

						continue;
					}

					_io.WriteLine();
					_io.Write("Explanation:\n", ConsoleColor.Green);
					_io.WriteLine(explanation);

					continue;

				case ConsoleKey.C:
					await _clipboard.SetTextAsync(suggestion, cancellationToken);
					_io.Write("Copied to clipboard\n", ConsoleColor.Green);

					return (ActionOutcome.Quit, null);

				case ConsoleKey.R:
					_io.Write("What should be different? ", ConsoleColor.Cyan);
					string? refinement = _io.ReadLine();

					return (ActionOutcome.Retry, refinement);

				case ConsoleKey.Q:
				case ConsoleKey.Escape:
					_io.WriteLine("Goodbye!");

					return (ActionOutcome.Quit, null);

				default:
					_io.Write("Invalid choice\n", ConsoleColor.Yellow);

					continue;
			}
		}
	}

	private void WriteOptions()
	{
		_io.Write("e[");
		_io.Write("x", ConsoleColor.Green);
		_io.Write("]ecute  [");
		_io.Write("e", ConsoleColor.Green);
		_io.Write("]xplain  [");
		_io.Write("c", ConsoleColor.Green);
		_io.Write("]opy  [");
		_io.Write("r", ConsoleColor.Green);
		_io.Write("]etry  [");
		_io.Write("q", ConsoleColor.Green);
		_io.Write("]uit ");
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
