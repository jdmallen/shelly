using JDMallen.Shelly.Tests.Fakes;
using Xunit;

namespace JDMallen.Shelly.Tests;

public class ReplLoopTests
{
	private readonly FakeChatProvider _provider = new();
	private readonly FakeConsoleIO _io = new();
	private readonly FakeClipboard _clipboard = new();

	private ReplLoop NewLoop(bool editEnabled = false) => new(_provider, _io, _clipboard, editEnabled);

	[Fact]
	public async Task Execute_ReturnsExecuteResultWithSuggestion()
	{
		_provider.SuggestionResponses.Enqueue("ls -la");
		_io.ReadKeys.Enqueue(ConsoleKey.X);

		ReplResult result = await NewLoop().RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.True(result.ShouldExecute);
		Assert.Equal("ls -la", result.Command);
		Assert.Single(_provider.SuggestionPrompts);
		Assert.Equal("list files", _provider.SuggestionPrompts[0]);
	}

	[Fact]
	public async Task Edit_WhenEnabled_ReturnsEditResultWithSuggestion()
	{
		_provider.SuggestionResponses.Enqueue("ls -la");
		_io.ReadKeys.Enqueue(ConsoleKey.E);

		ReplResult result = await NewLoop(editEnabled: true).RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.Equal(ReplAction.Edit, result.Action);
		Assert.False(result.ShouldExecute);
		Assert.Equal("ls -la", result.Command);
	}

	[Fact]
	public async Task Edit_WhenDisabled_KeyIsInvalidChoice()
	{
		_provider.SuggestionResponses.Enqueue("ls -la");
		_io.ReadKeys.Enqueue(ConsoleKey.E);
		_io.ReadKeys.Enqueue(ConsoleKey.Q);

		ReplResult result = await NewLoop(editEnabled: false).RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.Equal(ReplAction.Quit, result.Action);
		Assert.Contains("Invalid choice", _io.Output.ToString());
		Assert.DoesNotContain("]dit", _io.Output.ToString());
	}

	[Fact]
	public async Task Copy_WhenClipboardUnavailable_KeyIsInvalidAndOptionHidden()
	{
		_clipboard.IsAvailable = false;
		_provider.SuggestionResponses.Enqueue("ls -la");
		_io.ReadKeys.Enqueue(ConsoleKey.C);
		_io.ReadKeys.Enqueue(ConsoleKey.Q);

		ReplResult result = await NewLoop().RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.Equal(ReplAction.Quit, result.Action);
		Assert.Empty(_clipboard.SetTexts);
		Assert.Contains("Invalid choice", _io.Output.ToString());
		Assert.DoesNotContain("]opy", _io.Output.ToString());
	}

	[Fact]
	public async Task Quit_KeyReturnsQuitResult()
	{
		_provider.SuggestionResponses.Enqueue("ls -la");
		_io.ReadKeys.Enqueue(ConsoleKey.Q);

		ReplResult result = await NewLoop().RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.False(result.ShouldExecute);
		Assert.Null(result.Command);
	}

	[Fact]
	public async Task Escape_ReturnsQuitResult()
	{
		_provider.SuggestionResponses.Enqueue("ls -la");
		_io.ReadKeys.Enqueue(ConsoleKey.Escape);

		ReplResult result = await NewLoop().RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.False(result.ShouldExecute);
	}

	[Fact]
	public async Task Copy_SendsSuggestionToClipboardAndQuits()
	{
		_provider.SuggestionResponses.Enqueue("ls -la");
		_io.ReadKeys.Enqueue(ConsoleKey.C);

		ReplResult result = await NewLoop().RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.False(result.ShouldExecute);
		Assert.Single(_clipboard.SetTexts);
		Assert.Equal("ls -la", _clipboard.SetTexts[0]);
	}

	[Fact]
	public async Task Retry_WithRefinement_ReQueriesWithAppendedRefinement()
	{
		_provider.SuggestionResponses.Enqueue("ls");
		_provider.SuggestionResponses.Enqueue("ls -laSh");
		_io.ReadKeys.Enqueue(ConsoleKey.R);
		_io.ReadLines.Enqueue("by size");
		_io.ReadKeys.Enqueue(ConsoleKey.X);

		ReplResult result = await NewLoop().RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.True(result.ShouldExecute);
		Assert.Equal("ls -laSh", result.Command);
		Assert.Equal(2, _provider.SuggestionPrompts.Count);
		Assert.Equal("list files", _provider.SuggestionPrompts[0]);
		Assert.Equal("list files (refinement: by size)", _provider.SuggestionPrompts[1]);
	}

	[Fact]
	public async Task Retry_WithoutRefinement_ReQueriesWithOriginalPrompt()
	{
		_provider.SuggestionResponses.Enqueue("ls");
		_provider.SuggestionResponses.Enqueue("ls -la");
		_io.ReadKeys.Enqueue(ConsoleKey.R);
		_io.ReadLines.Enqueue("");
		_io.ReadKeys.Enqueue(ConsoleKey.X);

		ReplResult result = await NewLoop().RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.True(result.ShouldExecute);
		Assert.Equal("ls -la", result.Command);
		Assert.Equal(["list files", "list files"], _provider.SuggestionPrompts);
	}

	[Fact]
	public async Task Explain_CallsExplainAndRedisplaysOptionsWithoutNewSuggestion()
	{
		_provider.SuggestionResponses.Enqueue("ls -la");
		_provider.ExplanationResponses.Enqueue("Lists files in long format.");
		_io.ReadKeys.Enqueue(ConsoleKey.P);
		_io.ReadKeys.Enqueue(ConsoleKey.X);

		ReplResult result = await NewLoop().RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.True(result.ShouldExecute);
		Assert.Equal("ls -la", result.Command);
		Assert.Single(_provider.SuggestionPrompts);
		Assert.Single(_provider.ExplanationCommands);
		Assert.Equal("ls -la", _provider.ExplanationCommands[0]);
		Assert.Contains("Lists files in long format.", _io.Output.ToString());
	}

	[Fact]
	public async Task NullInitialPrompt_ReadsFromConsole()
	{
		_io.ReadLines.Enqueue("find large files");
		_provider.SuggestionResponses.Enqueue("du -ah | sort -h | tail");
		_io.ReadKeys.Enqueue(ConsoleKey.X);

		ReplResult result = await NewLoop().RunAsync(null, TestContext.Current.CancellationToken);

		Assert.True(result.ShouldExecute);
		Assert.Equal("find large files", _provider.SuggestionPrompts[0]);
	}

	[Fact]
	public async Task NullInitialPrompt_EmptyConsoleInput_Quits()
	{
		_io.ReadLines.Enqueue("");

		ReplResult result = await NewLoop().RunAsync(null, TestContext.Current.CancellationToken);

		Assert.False(result.ShouldExecute);
		Assert.Empty(_provider.SuggestionPrompts);
	}

	[Fact]
	public async Task EmptySuggestionFromProvider_Quits()
	{
		_provider.SuggestionResponses.Enqueue("");

		ReplResult result = await NewLoop().RunAsync("anything", TestContext.Current.CancellationToken);

		Assert.False(result.ShouldExecute);
		Assert.Contains("No suggestion received", _io.Output.ToString());
	}

	[Fact]
	public async Task ProviderThrowsOnSuggestion_QuitsWithError()
	{
		_provider.SuggestionThrow = new InvalidOperationException("network down");

		ReplResult result = await NewLoop().RunAsync("anything", TestContext.Current.CancellationToken);

		Assert.False(result.ShouldExecute);
		Assert.Contains("network down", _io.Output.ToString());
	}

	[Fact]
	public async Task ProviderThrowsOnExplain_StaysInActionLoop()
	{
		_provider.SuggestionResponses.Enqueue("ls -la");
		_provider.ExplanationThrow = new InvalidOperationException("explain broke");
		_io.ReadKeys.Enqueue(ConsoleKey.P);
		_io.ReadKeys.Enqueue(ConsoleKey.Q);

		ReplResult result = await NewLoop().RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.False(result.ShouldExecute);
		Assert.Contains("explain broke", _io.Output.ToString());
		Assert.Single(_provider.ExplanationCommands);
	}

	[Fact]
	public async Task UnknownKey_RedisplaysOptions()
	{
		_provider.SuggestionResponses.Enqueue("ls -la");
		_io.ReadKeys.Enqueue(ConsoleKey.A);
		_io.ReadKeys.Enqueue(ConsoleKey.Q);

		ReplResult result = await NewLoop().RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.False(result.ShouldExecute);
		Assert.Contains("Invalid choice", _io.Output.ToString());
		// Provider should have been queried exactly once — invalid key doesn't re-fetch.
		Assert.Single(_provider.SuggestionPrompts);
	}
}
