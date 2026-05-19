using System.Diagnostics;
using CommandLine;
using JDMallen.Shelly;
using JetBrains.Annotations;

const string apiKeyEnvVar = "ANTHROPIC_API_KEY_SHELLY";

return await Parser.Default
	.ParseArguments<Options>(args)
	.MapResult(RunAsync, _ => Task.FromResult(1));

static async Task<int> RunAsync(Options opts)
{
	LoadAnthropicCredsFile();

	if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(apiKeyEnvVar)))
	{
		Console.Error.WriteLine($"Error: {apiKeyEnvVar} environment variable not set");

		return 1;
	}

	string[] words = opts.Prompt?.ToArray() ?? [];
	string? prompt = words.Length > 0 ? string.Join(' ', words) : null;

	var provider = new AnthropicChatProvider();
	ReplResult result = await ReplLoop.RunAsync(provider, prompt);

	if (!result.ShouldExecute || string.IsNullOrWhiteSpace(result.Command))
	{
		return 0;
	}

	return await ExecuteAsync(result.Command);
}

static async Task<int> ExecuteAsync(string command)
{
	(string shell, string shellArg) = OperatingSystem.IsWindows()
		? ("cmd.exe", "/c")
		: (Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash", "-c");

	var startInfo = new ProcessStartInfo
	{
		FileName = shell,
		UseShellExecute = false,
	};
	startInfo.ArgumentList.Add(shellArg);
	startInfo.ArgumentList.Add(command);

	Console.ForegroundColor = ConsoleColor.Cyan;
	Console.WriteLine("Executing...");
	Console.ResetColor();

	Process? process = Process.Start(startInfo);
	if (process is null)
	{
		Console.Error.WriteLine("Error: failed to start shell");

		return 1;
	}

	await process.WaitForExitAsync();

	return process.ExitCode;
}

static void LoadAnthropicCredsFile()
{
	string? home = Environment.GetEnvironmentVariable("HOME");
	if (string.IsNullOrEmpty(home))
	{
		return;
	}

	string path = Path.Combine(home, ".anthropic_creds");
	if (!File.Exists(path))
	{
		return;
	}

	foreach (string line in File.ReadAllLines(path)
		         .Select(raw => raw.TrimStart())
		         .Where(line => !line.StartsWith('#') && line.Length != 0))
	{
		string ln = line;
		if (line.StartsWith("export ", StringComparison.Ordinal))
		{
			ln = ln[7..];
		}

		int eq = ln.IndexOf('=');
		if (eq <= 0)
		{
			continue;
		}

		string value = ln[(eq + 1)..].Trim().Trim('"', '\'');
		if (Environment.GetEnvironmentVariable(apiKeyEnvVar) is null)
		{
			Environment.SetEnvironmentVariable(apiKeyEnvVar, value);
		}
	}
}

[UsedImplicitly]
public sealed class Options
{
	[Value(0, MetaName = "prompt", HelpText = "Command description. If omitted, you'll be prompted.")]
	public IEnumerable<string>? Prompt
	{
		get;
		[UsedImplicitly]
		set;
	}
}
