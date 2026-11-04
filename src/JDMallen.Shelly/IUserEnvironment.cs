namespace JDMallen.Shelly;

/// <summary>
/// Reads environment variables and, where the OS keeps them outside shell
/// startup files, persists them for the user.
/// </summary>
internal interface IUserEnvironment
{
	/// <summary>
	/// Whether <see cref="SetPersistent" /> works here. True only on Windows,
	/// where user variables live in the registry; on Linux and macOS they live
	/// in shell startup files, which shelly doesn't edit.
	/// </summary>
	bool CanPersist { get; }

	string? Get(string name);

	/// <summary>
	/// Saves the variable for the user and sets it for this process, so it
	/// takes effect immediately as well as in programs started later.
	/// </summary>
	void SetPersistent(string name, string value);
}

internal sealed class SystemUserEnvironment : IUserEnvironment
{
	public bool CanPersist => OperatingSystem.IsWindows();

	public string? Get(string name) => Environment.GetEnvironmentVariable(name);

	public void SetPersistent(string name, string value)
	{
		if (!CanPersist)
		{
			throw new PlatformNotSupportedException();
		}

		Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.User);
		Environment.SetEnvironmentVariable(name, value);
	}
}
