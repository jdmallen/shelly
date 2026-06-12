using System.Diagnostics;
using CommandLine;
using JetBrains.Annotations;

namespace JDMallen.Shelly;

public static class Program
{
	// Shared transport for all chat completions; one REPL session, one client.
	private static readonly HttpClient HttpClient = new()
	{
		Timeout = TimeSpan.FromSeconds(30),
	};

	public static async Task<int> Main(string[] args)
	{
		return await Parser.Default
			.ParseArguments<Options>(args)
			.MapResult(RunAsync, _ => Task.FromResult(1));
	}

	private static async Task<int> RunAsync(Options opts)
	{
		ShellyConfig config = ShellyConfig.Load();

		IChatProvider? provider = CreateProvider(config);
		if (provider is null)
		{
			return 1;
		}

		string[] words = opts.Prompt?.ToArray() ?? [];
		string? prompt = words.Length > 0 ? string.Join(' ', words) : null;

		ReplResult result = await new ReplLoop(provider).RunAsync(prompt);

		if (string.IsNullOrWhiteSpace(result.Command))
		{
			return 0;
		}

		return result.Action switch
		{
			ReplAction.Execute => await ExecuteAsync(result.Command),
			ReplAction.Edit    => WriteEditHandoff(result.Command),
			_                  => 0,
		};
	}

	private static int WriteEditHandoff(string command)
	{
		string? path = EditHandoff.FilePath;
		if (string.IsNullOrEmpty(path))
		{
			// The edit action is only offered when the wrapper sets the env var,
			// so this is defensive: print the command so it isn't silently lost.
			Console.WriteLine(command);

			return 0;
		}

		File.WriteAllText(path, command);

		return 0;
	}

	private static IChatProvider? CreateProvider(ShellyConfig config)
	{
		return config.Provider.ToLowerInvariant() switch
		{
			"azure"     => CreateAzureProvider(),
			"anthropic" => CreateAnthropicProvider(config.Anthropic),
			_ => Fail(
				$"Unknown provider '{config.Provider}' in {ShellyConfig.ConfigPath()}. Expected 'anthropic' or 'azure'."),
		};
	}

	private static IChatProvider? CreateAnthropicProvider(AnthropicConfig cfg)
	{
		string? apiKey = Environment.GetEnvironmentVariable(ChatProvider.AnthropicApiKeyEnvVar);
		if (string.IsNullOrEmpty(apiKey))
		{
			return Fail($"{ChatProvider.AnthropicApiKeyEnvVar} environment variable not set");
		}

		return new ChatProvider(
			new AnthropicChatClient(
				HttpClient,
				new AnthropicClientOptions(apiKey, cfg.Model)));
	}

	private static IChatProvider? CreateAzureProvider()
	{
		string? apiKey = Environment.GetEnvironmentVariable(ChatProvider.AzureApiKeyEnvVar);
		string? endpoint = Environment.GetEnvironmentVariable(ChatProvider.AzureEndpointEnvVar);
		string? deployment = Environment.GetEnvironmentVariable(ChatProvider.AzureDeploymentEnvVar);

		var missing = new List<string>();
		if (string.IsNullOrEmpty(apiKey))
		{
			missing.Add(ChatProvider.AzureApiKeyEnvVar);
		}

		if (string.IsNullOrEmpty(endpoint))
		{
			missing.Add(ChatProvider.AzureEndpointEnvVar);
		}

		if (string.IsNullOrEmpty(deployment))
		{
			missing.Add(ChatProvider.AzureDeploymentEnvVar);
		}

		if (missing.Count > 0)
		{
			return Fail(
				$"Azure provider misconfigured. Missing env var(s): {string.Join(", ", missing)}.");
		}

		return new ChatProvider(
			new AzureOpenAIChatClient(
				HttpClient,
				new AzureOpenAIClientOptions(apiKey!, endpoint!, deployment!)));
	}

	private static IChatProvider? Fail(string message)
	{
		Console.Error.WriteLine($"Error: {message}");

		return null;
	}

	private static async Task<int> ExecuteAsync(string command)
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
			await Console.Error.WriteLineAsync("Error: failed to start shell");

			return 1;
		}

		await process.WaitForExitAsync();

		return process.ExitCode;
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
