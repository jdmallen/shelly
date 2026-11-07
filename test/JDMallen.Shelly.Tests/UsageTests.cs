using JDMallen.Shelly.Tests.Fakes;
using JDMallen.Toolbox.AI;

using Xunit;

namespace JDMallen.Shelly.Tests;

public sealed class UsageTests
{
	private readonly FakeChatProvider _provider = new();
	private readonly FakeConsoleIO _io = new();

	private ReplLoop NewLoop(bool showTokenUsage = true) => new(
		_provider,
		_io,
		new FakeClipboard(),
		false,
		new ShellContext(Shell.FromNameOrPath("bash"), "os", "/pwd"),
		showTokenUsage);

	[Fact]
	public async Task Repl_ShowsSessionTokensAfterTheMenu()
	{
		_provider.SuggestionResponses.Enqueue("ls -la");
		_provider.SuggestionUsages.Enqueue(new TokenUsage(1200, 34));
		_io.ReadKeys.Enqueue(ConsoleKey.Q);

		ReplLoop loop = NewLoop();
		await loop.RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.Contains("[q]uit   1,234 tokens", _io.Output.ToString());
		Assert.Equal(1, loop.SessionUsage.Requests);
		Assert.Equal(1234, loop.SessionUsage.TotalTokens);
	}

	[Fact]
	public async Task Repl_ExplanationAndRetry_AddToTheSessionTotal()
	{
		_provider.SuggestionResponses.Enqueue("ls");
		_provider.SuggestionUsages.Enqueue(new TokenUsage(100, 10));
		_provider.ExplanationResponses.Enqueue("lists files");
		_provider.ExplanationUsages.Enqueue(new TokenUsage(200, 20));
		_provider.SuggestionResponses.Enqueue("ls -la");
		_provider.SuggestionUsages.Enqueue(new TokenUsage(300, 30));
		_io.ReadKeys.Enqueue(ConsoleKey.P);
		_io.ReadKeys.Enqueue(ConsoleKey.R);
		_io.ReadLines.Enqueue("show hidden");
		_io.ReadKeys.Enqueue(ConsoleKey.Q);

		ReplLoop loop = NewLoop();
		await loop.RunAsync("list files", TestContext.Current.CancellationToken);

		string output = _io.Output.ToString();
		Assert.Contains("110 tokens", output);
		Assert.Contains("330 tokens", output);
		Assert.Contains("660 tokens", output);
		Assert.Equal(3, loop.SessionUsage.Requests);
		Assert.Equal(600, loop.SessionUsage.InputTokens);
		Assert.Equal(60, loop.SessionUsage.OutputTokens);
	}

	[Fact]
	public async Task Repl_ProviderReportsNoUsage_ShowsNoTokenCount()
	{
		_provider.SuggestionResponses.Enqueue("ls -la");
		_io.ReadKeys.Enqueue(ConsoleKey.Q);

		ReplLoop loop = NewLoop();
		await loop.RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.DoesNotContain("tokens", _io.Output.ToString());
		Assert.False(loop.SessionUsage.HasUsage);
	}

	[Fact]
	public async Task Repl_FooterTurnedOff_HidesTokensButStillTallies()
	{
		_provider.SuggestionResponses.Enqueue("ls -la");
		_provider.SuggestionUsages.Enqueue(new TokenUsage(1200, 34));
		_io.ReadKeys.Enqueue(ConsoleKey.Q);

		ReplLoop loop = NewLoop(false);
		await loop.RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.DoesNotContain("tokens", _io.Output.ToString());
		Assert.Equal(1234, loop.SessionUsage.TotalTokens);
	}

	[Fact]
	public async Task ChatProvider_PassesUsageThrough()
	{
		var provider = new ChatProvider(
			new FakeCompletionClient(
				new CompletionResult("```bash\nls -la\n```")
				{
					Usage = new TokenUsage(42, 7),
				}));

		ChatReply reply = await provider.GetSuggestionAsync(
			"list files",
			new ShellContext(Shell.FromNameOrPath("bash"), "os", "/pwd"),
			TestContext.Current.CancellationToken);

		Assert.Equal("ls -la", reply.Text);
		Assert.Equal(new TokenUsage(42, 7), reply.Usage);
	}

	private sealed class FakeCompletionClient(CompletionResult result)
		: IChatCompletionClient
	{
		[Obsolete("Shelly calls GetCompletionAsync.")]
		public Task<string> CompleteAsync(
			CompletionRequest request,
			CancellationToken cancellationToken = default)
			=> throw new NotSupportedException();

		public Task<CompletionResult> GetCompletionAsync(
			CompletionRequest request,
			CancellationToken cancellationToken = default)
			=> Task.FromResult(result);
	}
}
