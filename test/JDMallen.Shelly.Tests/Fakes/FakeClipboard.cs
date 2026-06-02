namespace JDMallen.Shelly.Tests.Fakes;

internal sealed class FakeClipboard : IClipboard
{
	public bool IsAvailable { get; set; } = true;

	public List<string> SetTexts { get; } = [];

	public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
	{
		SetTexts.Add(text);

		return Task.CompletedTask;
	}
}
