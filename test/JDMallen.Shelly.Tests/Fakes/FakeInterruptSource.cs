namespace JDMallen.Shelly.Tests.Fakes;

internal sealed class FakeInterruptSource : IInterruptSource
{
	private Func<bool>? _onInterrupt;

	public bool IsSubscribed => _onInterrupt is not null;

	public IDisposable Subscribe(Func<bool> onInterrupt)
	{
		_onInterrupt = onInterrupt;

		return new Subscription(this);
	}

	/// <summary>
	/// Presses Ctrl+C. Returns whether shelly would keep running.
	/// </summary>
	public bool Interrupt() =>
		_onInterrupt?.Invoke()
		?? throw new InvalidOperationException("Nobody is subscribed.");

	private sealed class Subscription(FakeInterruptSource source) : IDisposable
	{
		public void Dispose() => source._onInterrupt = null;
	}
}
