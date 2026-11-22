using System.Globalization;

namespace JDMallen.Shelly;

/// <summary>
/// The shell shelly writes commands for and runs them in.
/// </summary>
/// <param name="Name">
/// The shell's name as it was invoked, e.g. <c>zsh</c>, <c>dash</c>,
/// <c>pwsh</c>.
/// </param>
/// <param name="DisplayName">
/// The name the model sees, e.g. "Zsh", "POSIX sh (dash)", "PowerShell".
/// </param>
/// <param name="Executable">A path, or a name resolved on <c>PATH</c>.</param>
/// <param name="CommandFlags">
/// The arguments that make the shell run the single argument after them as a
/// command string, e.g. <c>-c</c> or <c>/c</c>.
/// </param>
/// <param name="PromptHint">
/// Extra syntax guidance for the model, for shells whose syntax a model is
/// likely to get wrong by defaulting to bash; null when none is needed.
/// </param>
internal sealed record Shell(
	string Name,
	string DisplayName,
	string Executable,
	IReadOnlyList<string> CommandFlags,
	string? PromptHint)
{
	private const string POSIX_HINT =
		"Use only POSIX sh features: no bash or zsh extensions such as [[ ]], "
		+ "arrays, brace expansion, `source`, `function` or `==` in tests.";

	private const string POWERSHELL_HINT =
		"Use PowerShell cmdlets and syntax (e.g. Get-ChildItem, Where-Object, "
		+ "Select-Object, $env:NAME), not POSIX shell syntax, unless a native "
		+ "executable is the natural tool for the job.";

	private const string CSH_HINT =
		"Use csh/tcsh syntax: `setenv NAME value` and `set name = value`, "
		+ "`foreach x (...) ... end`, `if (...) then ... endif`, backticks "
		+ "for command substitution; there is no `$(...)` and no `export`.";

	// Set-variable templates: {0} is the name, {1} the (already quoted) value.
	private const string EXPORT = "export {0}={1}";
	private const string SETENV = "setenv {0} {1}";
	private const string POWERSHELL_SET = "$env:{0} = {1}";

	private static readonly string[] DashC = ["-c"];

	private static readonly string[] PowerShellFlags =
		["-NoProfile", "-Command"];

	private static readonly KnownShell[] KnownShells =
	[
		new(["bash"], "Bash", DashC, null, EXPORT, "~/.bashrc"),
		new(["zsh"], "Zsh", DashC, null, EXPORT, "~/.zshrc"),
		new(
			["sh", "dash", "ash", "posh", "yash"],
			"POSIX sh",
			DashC,
			POSIX_HINT,
			EXPORT,
			"~/.profile"),
		new(
			["ksh", "ksh93", "mksh", "lksh", "oksh", "pdksh"],
			"KornShell",
			DashC,
			null,
			EXPORT,
			"~/.kshrc"),
		new(
			["fish"],
			"fish",
			DashC,
			"Use fish syntax, not POSIX or bash: `set NAME value` "
			+ "(`set -gx NAME value` to export), `(cmd)` or `$(cmd)` for command "
			+ "substitution, `if ...; end`, `for x in ...; end`, `and`/`or` or "
			+ "`&&`/`||`. There is no `export NAME=value`, no `$((...))` "
			+ "arithmetic (use `math`) and no heredocs.",
			"set -Ux {0} {1}",
			null),
		new(["tcsh"], "C shell (tcsh)", DashC, CSH_HINT, SETENV, "~/.tcshrc"),
		new(["csh"], "C shell (csh)", DashC, CSH_HINT, SETENV, "~/.cshrc"),
		new(
			["pwsh"],
			"PowerShell",
			PowerShellFlags,
			POWERSHELL_HINT,
			POWERSHELL_SET,
			"$PROFILE"),
		new(
			["powershell"],
			"Windows PowerShell 5.1",
			PowerShellFlags,
			POWERSHELL_HINT
			+ " This is Windows PowerShell 5.1, so `&&` and `||` do not exist: "
			+ "chain with `;` or `if ($?) { ... }`, and avoid PowerShell 7 "
			+ "features such as the ternary operator, `??` and "
			+ "`ForEach-Object -Parallel`.",
			POWERSHELL_SET,
			"$PROFILE"),
		new(
			["cmd"],
			"Windows Command Prompt (cmd.exe)",
			["/c"],
			"Use cmd.exe batch syntax: `%NAME%`, `set NAME=value`, `dir`, "
			+ "`copy`, `findstr`, `for %i in (...) do ...`; no PowerShell and no "
			+ "POSIX shell syntax.",
			null,
			null),
		new(
			["nu"],
			"Nushell",
			DashC,
			"Use Nushell syntax: structured pipelines (e.g. "
			+ "`ls | where size > 1mb | sort-by modified`), `$env.NAME`, `;` "
			+ "between commands, and Nushell builtins; no POSIX shell syntax, no "
			+ "`&&` and no `export`.",
			"$env.{0} = {1}",
			"$nu.env-path"),
		new(
			["elvish"],
			"Elvish",
			DashC,
			"Use Elvish syntax: `set E:NAME = value` for environment variables, "
			+ "`(cmd)` for output capture, `put`/`each` for structured values; "
			+ "no POSIX shell syntax.",
			"set E:{0} = {1}",
			"~/.config/elvish/rc.elv"),
	];

	/// <summary>
	/// Whether the shell is one shelly knows, rather than a name taken on
	/// trust from an override or <c>$SHELL</c>.
	/// </summary>
	public bool IsKnown => Find(Name) is not null;

	/// <summary>
	/// The line that sets <paramref name="variable" /> to
	/// <paramref name="value" /> in this shell, and the startup file it belongs
	/// in; a null file means the line is run once instead (fish's universal
	/// variables persist on their own). Null when the shell has no such
	/// convention: an unknown shell, or cmd, whose variables live in the
	/// registry.
	/// </summary>
	internal (string Line, string? StartupFile)? SetVariableLine(
		string variable,
		string value) =>
		Find(Name) is { SetVariable: { } template } known
			? (string.Format(
					CultureInfo.InvariantCulture,
					template,
					variable,
					value),
				known.StartupFile)
			: null;

	/// <summary>
	/// Resolves a known shell from a process name or executable path, e.g.
	/// <c>zsh</c>, <c>-zsh</c> (a login shell's argv[0]), <c>/bin/bash</c> or
	/// <c>C:\Program Files\PowerShell\7\pwsh.exe</c>.
	/// </summary>
	/// <param name="nameOrPath">The process name or executable path.</param>
	/// <param name="executablePath">
	/// The process's executable, when known. Used to run commands only when it
	/// is the shell itself: for a multi-call binary such as busybox, where the
	/// name is <c>sh</c> but the executable is <c>/bin/busybox</c>, the name is
	/// run instead.
	/// </param>
	internal static Shell? TryResolveKnown(
		string nameOrPath,
		string? executablePath = null)
	{
		string name = NormalizeName(nameOrPath);
		KnownShell? known = Find(name);
		if (known is null)
		{
			return null;
		}

		string executable = !string.IsNullOrEmpty(executablePath)
			&& NormalizeName(executablePath) == name
				? executablePath
				: LooksLikePath(nameOrPath)
					? nameOrPath
					: name;

		string displayName = known.DisplayName == "POSIX sh" && name != "sh"
			? $"POSIX sh ({name})"
			: known.DisplayName;

		return new Shell(
			name,
			displayName,
			executable,
			known.CommandFlags,
			known.PromptHint);
	}

	/// <summary>
	/// Resolves a shell from a name or path the user supplied (an override or
	/// <c>$SHELL</c>). An unknown shell is still honored: it is named to the
	/// model as-is and run with <c>-c</c>.
	/// </summary>
	internal static Shell FromNameOrPath(string nameOrPath)
	{
		Shell? known = TryResolveKnown(nameOrPath, nameOrPath);
		if (known is not null)
		{
			return known;
		}

		string name = NormalizeName(nameOrPath);

		return new Shell(
			name,
			name,
			nameOrPath,
			DashC,
			null);
	}

	/// <summary>
	/// Lower-cases a process name or path down to the bare shell name:
	/// directory, a login shell's leading <c>-</c> and a <c>.exe</c> suffix
	/// removed.
	/// </summary>
	private static string NormalizeName(string nameOrPath)
	{
		// Split on both separators: a Windows path must normalize on any OS.
		int lastSeparator = nameOrPath.LastIndexOfAny(['/', '\\']);
		string name = nameOrPath[(lastSeparator + 1)..].Trim().TrimStart('-');
		if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
		{
			name = name[..^4];
		}

		return name.ToLowerInvariant();
	}

	private static bool LooksLikePath(string nameOrPath) =>
		nameOrPath.IndexOfAny(['/', '\\']) >= 0;

	private static KnownShell? Find(string name) =>
		KnownShells.FirstOrDefault(known => known.Names.Contains(name));

	/// <param name="SetVariable">
	/// The set-variable template, or null when there is no startup-file
	/// convention.
	/// </param>
	/// <param name="StartupFile">
	/// Where that line goes; null when it is run once instead.
	/// </param>
	private sealed record KnownShell(
		string[] Names,
		string DisplayName,
		string[] CommandFlags,
		string? PromptHint,
		string? SetVariable,
		string? StartupFile);
}
