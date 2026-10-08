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
				_io.Write($"{Strings.WhatToDo} ", ConsoleColor.Cyan);
				string? line = _io.ReadLine();
				if (string.IsNullOrWhiteSpace(line))
				{
					return ReplResult.Quit();
				}

				prompt = line;
			}

			_io.Write($"{Strings.Thinking}\n", ConsoleColor.Cyan);
			string suggestion;
			try
			{
				suggestion = await _provider.GetSuggestionAsync(prompt, context, cancellationToken);
			}
			catch (Exception ex)
			{
				_io.Write($"{Strings.Error(ex.Message)}\n", ConsoleColor.Red);

				return ReplResult.Quit();
			}

			if (string.IsNullOrWhiteSpace(suggestion))
			{
				_io.Write($"{Strings.NoSuggestion}\n", ConsoleColor.Red);

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
				default:
#pragma warning disable CA2208
					throw new ArgumentOutOfRangeException(
						nameof(outcome),
						outcome,
						"Unexpected action outcome");
#pragma warning restore CA2208
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
			_io.Write($"{Strings.SuggestionHeader}\n", ConsoleColor.Green);
			_io.Write($"{suggestion}\n", ConsoleColor.Yellow);
			_io.WriteLine();
			WriteOptions();

			ConsoleKey choice = _io.ReadKey();
			_io.WriteLine();

			// ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
			switch (choice)
			{
				case ConsoleKey.X:
					return (ActionOutcome.Execute, null);

				case ConsoleKey.E when _editEnabled:
					return (ActionOutcome.Edit, null);

				case ConsoleKey.P:
					_io.Write($"{Strings.Explaining}\n", ConsoleColor.Cyan);
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
						_io.Write($"{Strings.Error(ex.Message)}\n", ConsoleColor.Red);

						continue;
					}

					_io.WriteLine();
					_io.Write($"{Strings.ExplanationHeader}\n", ConsoleColor.Green);
					_io.WriteLine(explanation);

					continue;

				case ConsoleKey.C when _clipboardEnabled:
					try
					{
						await _clipboard.SetTextAsync(suggestion, cancellationToken);
						_io.Write($"{Strings.CopiedToClipboard}\n", ConsoleColor.Green);

						return (ActionOutcome.Quit, null);
					}
					catch (Exception ex)
					{
						_io.Write($"{Strings.ClipboardUnavailable(ex.Message)}\n", ConsoleColor.Red);
						if (OperatingSystem.IsLinux())
						{
							_io.Write(
								$"{Strings.InstallClipboardToolLinux}\n",
								ConsoleColor.Yellow);
						}

						continue;
					}

				case ConsoleKey.R:
					_io.Write($"{Strings.WhatShouldBeDifferent} ", ConsoleColor.Cyan);
					string? refinement = _io.ReadLine();

					return (ActionOutcome.Retry, refinement);

				case ConsoleKey.Q:
				case ConsoleKey.Escape:
					_io.WriteLine(Strings.Goodbye);

					return (ActionOutcome.Quit, null);

				default:
					_io.Write($"{Strings.InvalidChoice}\n", ConsoleColor.Yellow);

					continue;
			}
		}
	}

	private void WriteOptions()
	{
		var first = true;

		Option('x', Strings.MenuExecute);
		if (_editEnabled)
		{
			Option('e', Strings.MenuEdit);
		}

		Option('p', Strings.MenuExplain);
		if (_clipboardEnabled)
		{
			Option('c', Strings.MenuCopy);
		}

		Option('r', Strings.MenuRetry);
		Option('q', Strings.MenuQuit);
		_io.Write(" ");

		return;

		// Each label carries its key as "[k]" (e.g. "e[x]ecute", "[x] ausführen");
		// the key is fixed across languages, so only the label around it varies.
		void Option(char key, string label)
		{
			if (!first)
			{
				_io.Write("  ");
			}

			first = false;
			string marker = $"[{key}]";
			int markerIndex = label.IndexOf(marker, StringComparison.Ordinal);
			(string before, string after) = markerIndex < 0
				? (string.Empty, $" {label}")
				: (label[..markerIndex], label[(markerIndex + marker.Length)..]);

			_io.Write($"{before}[");
			_io.Write(key.ToString(), ConsoleColor.Green);
			_io.Write($"]{after}");
		}
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
