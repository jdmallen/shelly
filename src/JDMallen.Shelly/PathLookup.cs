namespace JDMallen.Shelly;

internal static class PathLookup
{
	/// <summary>
	/// The first of <paramref name="names" /> found on <c>PATH</c>, or null.
	/// Directories are searched in order, trying every name in each, so an
	/// earlier entry wins the way it does in a shell.
	/// </summary>
	public static string? Find(
		Func<string, string?> getEnvironmentVariable,
		IReadOnlyList<string> names)
	{
		string? path = getEnvironmentVariable("PATH");
		if (string.IsNullOrEmpty(path))
		{
			return null;
		}

		return path
			.Split(
				Path.PathSeparator,
				StringSplitOptions.RemoveEmptyEntries
				| StringSplitOptions.TrimEntries)
			.SelectMany(_ => names, Path.Combine)
			.FirstOrDefault(File.Exists);
	}
}
