using TextCopy;

namespace JDMallen.Shelly;

internal interface IClipboard
{
	Task SetTextAsync(string text, CancellationToken cancellationToken = default);
}

internal sealed class TextCopyClipboard : IClipboard
{
	public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
		=> ClipboardService.SetTextAsync(text, cancellationToken);
}
