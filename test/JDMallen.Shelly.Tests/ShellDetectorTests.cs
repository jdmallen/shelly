using Xunit;

namespace JDMallen.Shelly.Tests;

public class ShellDetectorTests
{
	private const int SHELLY_ID = 500;

	private readonly Dictionary<string, string> _environment = new();

	private readonly FakeProcessTree _processTree = new();

	private Shell Detect(bool isWindows = false) =>
		ShellDetector.Detect(
			name => _environment.GetValueOrDefault(name),
			_processTree,
			SHELLY_ID,
			isWindows);

	/// <summary>
	/// Chains processes from shelly upward: the first entry is shelly's
	/// parent, the next its grandparent, and so on.
	/// </summary>
	private void GivenAncestors(params (string Name, string? Path)[] ancestors)
	{
		int processId = SHELLY_ID;
		_processTree.Add(processId, processId + 1, "shelly", "/usr/local/bin/shelly");
		foreach ((string name, string? path) in ancestors)
		{
			processId++;
			_processTree.Add(processId, processId + 1, name, path);
		}
	}

	[Fact]
	public void ParentShell_IsDetected()
	{
		GivenAncestors(("bash", "/usr/bin/bash"), ("zsh", "/usr/bin/zsh"));
		_environment["SHELL"] = "/usr/bin/zsh";

		Shell shell = Detect();

		Assert.Equal("bash", shell.Name);
		Assert.Equal("/usr/bin/bash", shell.Executable);
		Assert.Equal(["-c"], shell.CommandFlags);
	}

	[Fact]
	public void NonShellAncestors_AreSkipped()
	{
		GivenAncestors(
			("dotnet", "/usr/lib/dotnet/dotnet"),
			("sudo", "/usr/bin/sudo"),
			("fish", "/usr/bin/fish"));

		Assert.Equal("fish", Detect().Name);
	}

	[Fact]
	public void LoginShellName_WithLeadingDash_IsDetected()
	{
		GivenAncestors(("-zsh", null));

		Shell shell = Detect();

		Assert.Equal("zsh", shell.Name);
		Assert.Equal("zsh", shell.Executable);
	}

	[Fact]
	public void MultiCallBinary_RunsTheNameNotTheBinary()
	{
		GivenAncestors(("sh", "/bin/busybox"));

		Shell shell = Detect();

		Assert.Equal("sh", shell.Name);
		Assert.Equal("sh", shell.Executable);
	}

	[Fact]
	public void UnrecognizedName_FallsBackToExecutablePath()
	{
		GivenAncestors(("weird-comm", "/opt/homebrew/bin/fish"));

		Shell shell = Detect();

		Assert.Equal("fish", shell.Name);
		Assert.Equal("/opt/homebrew/bin/fish", shell.Executable);
	}

	[Theory]
	[InlineData(@"C:\Program Files\PowerShell\7\pwsh.exe", "pwsh", "PowerShell")]
	[InlineData(
		@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
		"powershell",
		"Windows PowerShell 5.1")]
	[InlineData(@"C:\Windows\System32\cmd.exe", "cmd", "Windows Command Prompt (cmd.exe)")]
	public void WindowsShell_IsDetected(string path, string name, string displayName)
	{
		GivenAncestors((Path.GetFileName(path.Replace('\\', '/')), path));

		Shell shell = Detect(isWindows: true);

		Assert.Equal(name, shell.Name);
		Assert.Equal(displayName, shell.DisplayName);
		Assert.Equal(path, shell.Executable);
	}

	[Fact]
	public void PowerShell_RunsWithNoProfileCommand()
	{
		GivenAncestors(("pwsh", "/usr/bin/pwsh"));

		Assert.Equal(["-NoProfile", "-Command"], Detect().CommandFlags);
	}

	[Fact]
	public void Override_WinsOverAncestors()
	{
		GivenAncestors(("bash", "/usr/bin/bash"));
		_environment[ShellDetector.OVERRIDE_ENV_VAR] = "fish";

		Shell shell = Detect();

		Assert.Equal("fish", shell.Name);
		Assert.Equal("fish", shell.Executable);
	}

	[Fact]
	public void Override_UnknownShell_IsHonoredWithDashC()
	{
		_environment[ShellDetector.OVERRIDE_ENV_VAR] = "/usr/bin/xonsh";

		Shell shell = Detect();

		Assert.False(shell.IsKnown);
		Assert.Equal("xonsh", shell.DisplayName);
		Assert.Equal("/usr/bin/xonsh", shell.Executable);
		Assert.Equal(["-c"], shell.CommandFlags);
	}

	[Fact]
	public void NoShellAncestor_FallsBackToShellVariable()
	{
		GivenAncestors(("node", "/usr/bin/node"));
		_environment["SHELL"] = "/usr/bin/zsh";

		Shell shell = Detect();

		Assert.Equal("zsh", shell.Name);
		Assert.Equal("/usr/bin/zsh", shell.Executable);
	}

	[Fact]
	public void NoShellAncestor_NoShellVariable_FallsBackToBinSh()
	{
		Assert.Equal("/bin/sh", Detect().Executable);
	}

	[Fact]
	public void NoShellAncestor_OnWindows_FallsBackToComSpec()
	{
		_environment["ComSpec"] = @"C:\Windows\system32\cmd.exe";
		_environment["SHELL"] = "/usr/bin/bash";

		Shell shell = Detect(isWindows: true);

		Assert.Equal("cmd", shell.Name);
		Assert.Equal(["/c"], shell.CommandFlags);
	}

	[Fact]
	public void ProcessCycle_DoesNotLoopForever()
	{
		_processTree.Add(SHELLY_ID, 600, "shelly", null);
		_processTree.Add(600, 601, "node", null);
		_processTree.Add(601, 600, "node", null);
		_environment["SHELL"] = "/bin/bash";

		Assert.Equal("bash", Detect().Name);
	}

	[Fact]
	public void ParseParentId_CommWithSpacesAndParens()
	{
		Assert.Equal(42, LinuxProcessTree.ParseParentId("123 (my (odd) proc) S 42 123 123 0"));
	}

	[Fact]
	public void LinuxProcessTree_ReadsThisProcess()
	{
		Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only");

		ProcessEntry? self = new LinuxProcessTree().Get(Environment.ProcessId);

		Assert.NotNull(self);
		Assert.True(self.Value.ParentId > 0);
		Assert.False(string.IsNullOrEmpty(self.Value.Name));
	}

	private sealed class FakeProcessTree : IProcessTree
	{
		private readonly Dictionary<int, ProcessEntry> _entries = new();

		public void Add(int processId, int parentId, string name, string? path) =>
			_entries[processId] = new ProcessEntry(parentId, name, path);

		public ProcessEntry? Get(int processId) =>
			_entries.TryGetValue(processId, out ProcessEntry entry) ? entry : null;
	}
}
