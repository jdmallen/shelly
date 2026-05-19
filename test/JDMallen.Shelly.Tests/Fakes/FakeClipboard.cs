using JDMallen.Shelly;

namespace JDMallen.Shelly.Tests.Fakes;

internal sealed class FakeClipboard : IClipboard
{
	public List<string> SetTexts { get; } = [];

	public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
	{
		SetTexts.Add(text);

		return Task.CompletedTask;
	}
}
