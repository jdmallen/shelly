namespace JDMallen.Shelly.Tests.Fakes;

internal sealed class FakeUserEnvironment(bool canPersist = false)
	: IUserEnvironment
{
	public Dictionary<string, string> Variables { get; } = new();

	public Dictionary<string, string> Persisted { get; } = new();

	public bool CanPersist => canPersist;

	public string? Get(string name) => Variables.GetValueOrDefault(name);

	public void SetPersistent(string name, string value)
	{
		Persisted[name] = value;
		Variables[name] = value;
	}
}
