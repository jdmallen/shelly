namespace JDMallen.Shelly;

internal sealed class ReplLoop
{
	private readonly IChatProvider _provider;
	private readonly IConsoleIO _io;
	private readonly IClipboard _clipboard;
	private readonly bool _editEnabled;
	private readonly bool _clipboardEnabled;
	private readonly bool _showTokenUsage;
	private readonly ShellContext _context;

	internal ReplLoop(
		IChatProvider provider,
		IConsoleIO io,
		IClipboard clipboard,
		bool editEnabled = false,
		ShellContext? context = null,
		bool showTokenUsage = true)
	{
		_provider = provider;
		_context = context ?? ShellContext.Detect();
		_io = io;
		_clipboard = clipboard;
		_editEnabled = editEnabled;
		_clipboardEnabled = clipboard.IsAvailable;
		_showTokenUsage = showTokenUsage;
	}

	public ReplLoop(
		IChatProvider provider,
		IConsoleIO io,
		ShellContext context,
		bool showTokenUsage = true)
		: this(
			provider,
			io,
			new SystemClipboard(),
			EditHandoff.IsEnabled,
			context,
			showTokenUsage)
	{
	}

	/// <summary>
	/// Token usage of every model request this loop has made so far.
	/// </summary>
	public TokenUsageTally SessionUsage { get; } = new();

	// Ctrl+C records SessionUsage from another thread.
	private readonly Lock _usageLock = new();

	/// <summary>
	/// Passes <see cref="SessionUsage" /> to <paramref name="read" /> while no
	/// request's usage is being added to it.
	/// </summary>
	public void ReadUsage(Action<TokenUsageTally> read)
	{
		lock (_usageLock)
		{
			read(SessionUsage);
		}
	}

	private void AddUsage(TokenUsage? usage)
	{
		lock (_usageLock)
		{
			SessionUsage.Add(usage);
		}
	}

	public async Task<ReplResult> RunAsync(
		string? initialPrompt,
		CancellationToken cancellationToken = default)
	{
		string? prompt = initialPrompt;

		while (true)
		{
			// Piped input gets only the command on stdout, so it composes with
			// other tools: no progress line, no menu.
			bool interactive = _io.IsInteractive;
			if (string.IsNullOrWhiteSpace(prompt))
			{
				if (interactive)
				{
					_io.Write($"{Strings.WhatToDo} ", ConsoleColor.Cyan);
				}
				else if (!_io.IsInputRedirected)
				{
					// Only stdout is captured (x=$(shelly)): someone is
					// typing, so ask on stderr, out of the captured output.
					_io.WriteErrorInline($"{Strings.WhatToDo} ");
				}

				// Piped input is read whole: a prompt may span several lines.
				string? line = _io.IsInputRedirected
					? _io.ReadToEnd()
					: _io.ReadLine();
				if (string.IsNullOrWhiteSpace(line))
				{
					return ReplResult.Quit();
				}

				prompt = line.Trim();
			}

			if (interactive)
			{
				_io.WriteLine(Strings.Thinking, ConsoleColor.Cyan);
			}

			string suggestion;
			try
			{
				ChatReply reply = await _provider.GetSuggestionAsync(
					prompt,
					_context,
					cancellationToken);
				AddUsage(reply.Usage);
				if (reply.Truncated)
				{
					return Fail(Strings.ResponseTruncated, interactive);
				}

				suggestion = reply.Text;
			}
			catch (Exception ex)
			{
				return Fail(Strings.Error(ex.Message), interactive);
			}

			if (string.IsNullOrWhiteSpace(suggestion))
			{
				return Fail(Strings.NoSuggestion, interactive);
			}

			if (!interactive)
			{
				// Captured output is data and stays exact; on a terminal
				// (only stdin piped) someone reads it, so escape it.
				_io.WriteLine(
					_io.IsOutputRedirected
						? suggestion
						: TerminalText.Escape(suggestion));
				if (TerminalText.HasHiddenCharacters(suggestion))
				{
					// stderr, so it never becomes part of a captured command.
					_io.WriteError(Strings.HiddenCharactersWarning);
				}

				return ReplResult.Quit();
			}

			(ActionOutcome outcome, string? refinement) =
				await ActOnSuggestionAsync(
					suggestion,
					_context,
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
					prompt = Prompts.Retry(prompt, suggestion, refinement);

					continue;
				default:
#pragma warning disable CA2208
					throw new ArgumentOutOfRangeException(
						nameof(outcome),
						outcome,
						@"Unexpected action outcome");
#pragma warning restore CA2208
			}
		}
	}

	/// <summary>
	/// Reports a failed request: in red on the terminal, or on stderr when
	/// stdout is piped or captured, so it never passes for a command.
	/// </summary>
	private ReplResult Fail(string message, bool interactive)
	{
		// The message may carry a server's error body.
		message = TerminalText.Escape(message);
		if (interactive)
		{
			_io.WriteLine(message, ConsoleColor.Red);
		}
		else
		{
			_io.WriteError(message);
		}

		return ReplResult.Failed();
	}

	private async Task<(ActionOutcome, string?)> ActOnSuggestionAsync(
		string suggestion,
		ShellContext context,
		CancellationToken cancellationToken)
	{
		while (true)
		{
			_io.WriteLine();
			_io.WriteLine(Strings.SuggestionHeader, ConsoleColor.Green);
			// Escaped for display only: x, e and c use the exact text.
			_io.WriteLine(TerminalText.Escape(suggestion), ConsoleColor.Yellow);
			if (TerminalText.HasHiddenCharacters(suggestion))
			{
				// Copy keeps them too, and a raw CR pasted into a shell
				// without bracketed paste (e.g. tcsh) acts as Enter.
				_io.WriteLine(
					Strings.HiddenCharactersWarning,
					ConsoleColor.Red);
			}
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
					_io.WriteLine(Strings.Explaining, ConsoleColor.Cyan);
					string explanation;
					try
					{
						ChatReply reply = await _provider.ExplainCommandAsync(
							suggestion,
							context,
							cancellationToken);
						AddUsage(reply.Usage);
						if (reply.Truncated)
						{
							_io.WriteLine(
								Strings.ResponseTruncated,
								ConsoleColor.Red);

							continue;
						}

						explanation = reply.Text;
					}
					catch (Exception ex)
					{
						_io.WriteLine(
							TerminalText.Escape(Strings.Error(ex.Message)),
							ConsoleColor.Red);

						continue;
					}

					_io.WriteLine();
					_io.WriteLine(
						Strings.ExplanationHeader,
						ConsoleColor.Green);
					_io.WriteLine(TerminalText.Escape(explanation));

					continue;

				case ConsoleKey.C when _clipboardEnabled:
					try
					{
						await _clipboard.SetTextAsync(
							suggestion,
							cancellationToken);
						_io.WriteLine(
							Strings.CopiedToClipboard,
							ConsoleColor.Green);

						return (ActionOutcome.Quit, null);
					}
					catch (Exception ex)
					{
						_io.WriteLine(
							TerminalText.Escape(
								Strings.ClipboardUnavailable(ex.Message)),
							ConsoleColor.Red);
						if (OperatingSystem.IsLinux())
						{
							_io.WriteLine(
								Strings.InstallClipboardToolLinux,
								ConsoleColor.Yellow);
						}

						continue;
					}

				case ConsoleKey.R:
					_io.Write(
						$"{Strings.WhatShouldBeDifferent} ",
						ConsoleColor.Cyan);
					string? refinement = _io.ReadLine();

					return (ActionOutcome.Retry, refinement);

				case ConsoleKey.Q:
				case ConsoleKey.Escape:
					_io.WriteLine(Strings.Goodbye);

					return (ActionOutcome.Quit, null);

				default:
					_io.WriteLine(
						Strings.InvalidChoice,
						ConsoleColor.Yellow);

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

		// Faded, so it reads as a footnote to the menu rather than an option.
		if (_showTokenUsage && SessionUsage.HasUsage)
		{
			_io.Write(
				$"   {Strings.TokensUsed(SessionUsage.TotalTokens)}",
				ConsoleColor.DarkGray);
		}

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
			var marker = $"[{key}]";
			int markerIndex = label.IndexOf(marker, StringComparison.Ordinal);
			(string before, string after) = markerIndex < 0
				? (string.Empty, $" {label}")
				: (label[..markerIndex],
					label[(markerIndex + marker.Length)..]);

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

internal enum ReplAction
{
	Quit,
	Execute,
	Edit,

	/// <summary>The model call failed or gave no suggestion; exit non-zero.</summary>
	Failed,
}

internal readonly record struct ReplResult(ReplAction Action, string? Command)
{
	public bool ShouldExecute => Action == ReplAction.Execute;

	public static ReplResult Execute(string command)
		=> new(ReplAction.Execute, command);

	public static ReplResult Edit(string command)
		=> new(ReplAction.Edit, command);

	public static ReplResult Quit() => new(ReplAction.Quit, null);

	public static ReplResult Failed() => new(ReplAction.Failed, null);
}
