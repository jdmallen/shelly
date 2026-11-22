namespace JDMallen.Shelly;

/// <summary>
/// Ctrl+C (SIGINT) or Ctrl+Break. A seam over
/// <see cref="Console.CancelKeyPress" />, whose event args can't be built in
/// a test.
/// </summary>
internal interface IInterruptSource
{
	/// <summary>
	/// Calls <paramref name="onInterrupt" /> on each interrupt until the
	/// returned handle is disposed. It runs on another thread and returns
	/// true to keep shelly running, false to let the interrupt end it.
	/// </summary>
	IDisposable Subscribe(Func<bool> onInterrupt);
}

internal sealed class SystemInterruptSource : IInterruptSource
{
	public IDisposable Subscribe(Func<bool> onInterrupt)
	{
		ConsoleCancelEventHandler handler =
			(_, eventArgs) => eventArgs.Cancel = onInterrupt();
		Console.CancelKeyPress += handler;

		return new Subscription(handler);
	}

	private sealed class Subscription(ConsoleCancelEventHandler handler)
		: IDisposable
	{
		public void Dispose() => Console.CancelKeyPress -= handler;
	}
}
