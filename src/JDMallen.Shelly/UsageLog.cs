using System.Text.Json;
using System.Text.Json.Serialization;

namespace JDMallen.Shelly;

/// <summary>
/// Lifetime token totals across every shelly run, kept in <c>usage.json</c>
/// beside the other local app data (not in the config directory: it is state,
/// not settings).
/// </summary>
public sealed class UsageTotals
{
	/// <summary>Runs that made at least one request reporting usage.</summary>
	public int Runs { get; set; }

	public long Requests { get; set; }

	public long InputTokens { get; set; }

	public long OutputTokens { get; set; }

	/// <summary>When the first run was recorded.</summary>
	public DateTimeOffset? Since { get; set; }

	[JsonIgnore]
	public long TotalTokens => InputTokens + OutputTokens;
}

internal static class UsageLog
{
	private const int LOCK_ATTEMPTS = 40;
	private static readonly TimeSpan LockRetryDelay =
		TimeSpan.FromMilliseconds(50);

	public static string FilePath()
	{
		string baseDirectory = Environment.GetFolderPath(
			Environment.SpecialFolder.LocalApplicationData);
		if (string.IsNullOrEmpty(baseDirectory))
		{
			string? home = Environment.GetEnvironmentVariable("HOME");
			baseDirectory = string.IsNullOrEmpty(home)
				? "."
				: Path.Combine(home, ".local", "share");
		}

		return Path.Combine(baseDirectory, "shelly", "usage.json");
	}

	/// <summary>
	/// Reads the totals; a missing file yields empty totals. A file that can't
	/// be read or parsed yields null, with the reason in <paramref name="error" />.
	/// </summary>
	public static UsageTotals? Read(string path, out string? error)
	{
		error = null;
		try
		{
			return ReadUnlocked(path);
		}
		catch (Exception ex) when (ex is IOException
			                           or UnauthorizedAccessException
			                           or JsonException)
		{
			error = ex.Message;

			return null;
		}
	}

	/// <summary>
	/// Adds one run's usage to the totals at <paramref name="path" />. Runs
	/// that reported no usage are not counted, so they don't drag the average
	/// down. Concurrent shelly processes serialize on a lock file, and the
	/// update is written via a temporary file, so an interrupted write can't
	/// truncate the totals. An unparseable file is left untouched rather than
	/// overwritten.
	/// </summary>
	/// <returns>False, with the reason in <paramref name="error" />, on failure.</returns>
	public static bool TryRecord(
		string path,
		UsageTally session,
		DateTimeOffset now,
		out string? error)
	{
		error = null;
		if (!session.HasUsage)
		{
			return true;
		}

		try
		{
			string directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
			Directory.CreateDirectory(directory);
			using FileStream lockStream = AcquireLock($"{path}.lock");

			UsageTotals totals = ReadUnlocked(path);
			totals.Runs++;
			totals.Requests += session.Requests;
			totals.InputTokens += session.InputTokens;
			totals.OutputTokens += session.OutputTokens;
			totals.Since ??= now;

			Write(path, directory, totals);

			return true;
		}
		catch (Exception ex) when (ex is IOException
			                           or UnauthorizedAccessException
			                           or JsonException)
		{
			error = ex.Message;

			return false;
		}
	}

	private static UsageTotals ReadUnlocked(string path)
	{
		if (!File.Exists(path))
		{
			return new UsageTotals();
		}

		using FileStream stream = File.OpenRead(path);

		return JsonSerializer.Deserialize(
			       stream,
			       UsageLogJsonContext.Default.UsageTotals)
		       ?? new UsageTotals();
	}

	private static void Write(
		string path,
		string directory,
		UsageTotals totals)
	{
		string temporaryPath = Path.Combine(
			directory,
			$".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
		try
		{
			using (FileStream stream = File.Create(temporaryPath))
			{
				JsonSerializer.Serialize(
					stream,
					totals,
					UsageLogJsonContext.Default.UsageTotals);
			}

			File.Move(temporaryPath, path, true);
		}
		finally
		{
			File.Delete(temporaryPath);
		}
	}

	// FileShare.None takes an exclusive lock (flock on Unix), held until the
	// stream is disposed. The file's contents are never used.
	private static FileStream AcquireLock(string lockPath)
	{
		for (var attempt = 1; ; attempt++)
		{
			try
			{
				return new FileStream(
					lockPath,
					FileMode.OpenOrCreate,
					FileAccess.ReadWrite,
					FileShare.None);
			}
			catch (IOException) when (attempt < LOCK_ATTEMPTS)
			{
				Thread.Sleep(LockRetryDelay);
			}
		}
	}
}

[JsonSourceGenerationOptions(
	WriteIndented = true,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UsageTotals))]
internal partial class UsageLogJsonContext : JsonSerializerContext;
