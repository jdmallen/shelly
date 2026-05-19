using System.Diagnostics;
using CommandLine;
using JDMallen.Shelly;

return await Parser.Default
    .ParseArguments<Options>(args)
    .MapResult(RunAsync, errors => Task.FromResult(1));

static async Task<int> RunAsync(Options opts)
{
    LoadAnthropicCredsFile();

    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
    {
        Console.Error.WriteLine("Error: ANTHROPIC_API_KEY environment variable not set");
        return 1;
    }

    var words = opts.Prompt?.ToArray() ?? [];
    var prompt = words.Length > 0 ? string.Join(' ', words) : null;

    var provider = new AnthropicChatProvider();
    var result = await ReplLoop.RunAsync(provider, prompt);

    if (!result.ShouldExecute || string.IsNullOrWhiteSpace(result.Command))
    {
        return 0;
    }

    return await ExecuteAsync(result.Command);
}

static async Task<int> ExecuteAsync(string command)
{
    var (shell, shellArg) = OperatingSystem.IsWindows()
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

    var process = Process.Start(startInfo);
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
    var home = Environment.GetEnvironmentVariable("HOME");
    if (string.IsNullOrEmpty(home)) return;
    var path = Path.Combine(home, ".anthropic_creds");
    if (!File.Exists(path)) return;

    foreach (var raw in File.ReadAllLines(path))
    {
        var line = raw.TrimStart();
        if (line.StartsWith('#') || line.Length == 0) continue;
        if (line.StartsWith("export ", StringComparison.Ordinal)) line = line[7..];

        var eq = line.IndexOf('=');
        if (eq <= 0) continue;

        var key = line[..eq].Trim();
        var value = line[(eq + 1)..].Trim().Trim('"', '\'');
        if (Environment.GetEnvironmentVariable(key) is null)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

public sealed class Options
{
    [Value(0, MetaName = "prompt", HelpText = "Command description. If omitted, you'll be prompted.")]
    public IEnumerable<string>? Prompt { get; set; }
}
