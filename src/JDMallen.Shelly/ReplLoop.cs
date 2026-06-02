namespace JDMallen.Shelly;

public sealed class ReplLoop
{
	private readonly IChatProvider _provider;
	private readonly IConsoleIO _io;
	private readonly IClipboard _clipboard;
	private readonly bool _editEnabled;
	private readonly bool _clipboardEnabled;

	internal ReplLoop(
		IChatProvider provider,
		IConsoleIO io,
		IClipboard clipboard,
		bool editEnabled = false)
	{
		_provider = provider;
		_io = io;
		_clipboard = clipboard;
		_editEnabled = editEnabled;
		_clipboardEnabled = clipboard.IsAvailable;
	}

	public ReplLoop(IChatProvider provider)
		: this(
			provider,
			new SystemConsoleIO(),
			new SystemClipboard(),
			EditHandoff.IsEnabled)
	{
	}

	public async Task<ReplResult> RunAsync(
		string? initialPrompt,
		CancellationToken cancellationToken = default)
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
				case ActionOutcome.Edit:
					return ReplResult.Edit(suggestion);
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

				case ConsoleKey.E when _editEnabled:
					return (ActionOutcome.Edit, null);

				case ConsoleKey.P:
					_io.Write("Explaining...\n", ConsoleColor.Cyan);
					string explanation;
					try
					{
						explanation = await _provider.ExplainCommandAsync(
							suggestion,
							context,
							cancellationToken);
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

				case ConsoleKey.C when _clipboardEnabled:
					try
					{
						await _clipboard.SetTextAsync(suggestion, cancellationToken);
						_io.Write("Copied to clipboard\n", ConsoleColor.Green);

						return (ActionOutcome.Quit, null);
					}
					catch (Exception ex)
					{
						_io.Write($"Clipboard unavailable: {ex.Message}\n", ConsoleColor.Red);
						if (OperatingSystem.IsLinux())
						{
							_io.Write(
								"Install xclip, xsel, or wl-clipboard to enable copying on Linux.\n",
								ConsoleColor.Yellow);
						}

						continue;
					}

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
		bool first = true;

		void Option(string before, char key, string after)
		{
			if (!first)
			{
				_io.Write("  ");
			}

			first = false;
			_io.Write($"{before}[");
			_io.Write(key.ToString(), ConsoleColor.Green);
			_io.Write($"]{after}");
		}

		Option("e", 'x', "ecute");
		if (_editEnabled)
		{
			Option(string.Empty, 'e', "dit");
		}

		Option("ex", 'p', "lain");
		if (_clipboardEnabled)
		{
			Option(string.Empty, 'c', "opy");
		}

		Option(string.Empty, 'r', "etry");
		Option(string.Empty, 'q', "uit");
		_io.Write(" ");
	}

	private enum ActionOutcome
	{
		Execute,
		Edit,
		Retry,
		Quit,
	}
}

public enum ReplAction
{
	Quit,
	Execute,
	Edit,
}

public readonly record struct ReplResult(ReplAction Action, string? Command)
{
	public bool ShouldExecute => Action == ReplAction.Execute;

	public static ReplResult Execute(string command) => new(ReplAction.Execute, command);

	public static ReplResult Edit(string command) => new(ReplAction.Edit, command);

	public static ReplResult Quit() => new(ReplAction.Quit, null);
}
