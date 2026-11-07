using JDMallen.Shelly.Tests.Fakes;
using JDMallen.Toolbox.AI;

using Xunit;

namespace JDMallen.Shelly.Tests;

public sealed class UsageTests : IDisposable
{
	private readonly FakeChatProvider _provider = new();
	private readonly FakeConsoleIO _io = new();
	private readonly string _directory = Path.Combine(
		Path.GetTempPath(),
		$"shelly-usage-{Guid.NewGuid():N}");

	private string UsagePath => Path.Combine(_directory, "usage.json");

	public void Dispose()
	{
		if (Directory.Exists(_directory))
		{
			Directory.Delete(_directory, true);
		}
	}

	private ReplLoop NewLoop() => new(
		_provider,
		_io,
		new FakeClipboard(),
		false,
		new ShellContext(Shell.FromNameOrPath("bash"), "os", "/pwd"));

	private static UsageTally Tally(params TokenUsage?[] usages)
	{
		var tally = new UsageTally();
		foreach (TokenUsage? usage in usages)
		{
			tally.Add(usage);
		}

		return tally;
	}

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
	public void Read_MissingFile_ReturnsEmptyTotals()
	{
		UsageTotals? totals = UsageLog.Read(UsagePath, out string? error);

		Assert.Null(error);
		Assert.NotNull(totals);
		Assert.Equal(0, totals.Runs);
	}

	[Fact]
	public void TryRecord_AccumulatesAcrossRuns()
	{
		var firstRun = new DateTimeOffset(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

		Assert.True(
			UsageLog.TryRecord(
				UsagePath,
				Tally(new TokenUsage(100, 10), new TokenUsage(50, 5)),
				firstRun,
				out _));
		Assert.True(
			UsageLog.TryRecord(
				UsagePath,
				Tally(new TokenUsage(200, 20)),
				firstRun.AddDays(1),
				out _));

		UsageTotals totals = UsageLog.Read(UsagePath, out _)!;
		Assert.Equal(2, totals.Runs);
		Assert.Equal(3, totals.Requests);
		Assert.Equal(350, totals.InputTokens);
		Assert.Equal(35, totals.OutputTokens);
		Assert.Equal(firstRun, totals.Since);
		Assert.Empty(
			Directory.GetFiles(_directory, "*.tmp", SearchOption.AllDirectories));
	}

	[Fact]
	public void TryRecord_RunWithoutUsage_IsNotCounted()
	{
		Assert.True(
			UsageLog.TryRecord(
				UsagePath,
				Tally((TokenUsage?)null),
				DateTimeOffset.Now,
				out _));

		Assert.False(File.Exists(UsagePath));
	}

	[Fact]
	public void TryRecord_UnparseableFile_IsLeftUntouched()
	{
		Directory.CreateDirectory(_directory);
		File.WriteAllText(UsagePath, "{ not json");

		bool recorded = UsageLog.TryRecord(
			UsagePath,
			Tally(new TokenUsage(1, 1)),
			DateTimeOffset.Now,
			out string? error);

		Assert.False(recorded);
		Assert.NotNull(error);
		Assert.Equal("{ not json", File.ReadAllText(UsagePath));
		Assert.Null(UsageLog.Read(UsagePath, out string? readError));
		Assert.NotNull(readError);
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
