using System.Runtime.InteropServices;
using System.Text;

namespace JDMallen.Shelly;

/// <param name="ParentId">The parent's process id; 0 when it has none.</param>
/// <param name="Name">The process name, e.g. <c>zsh</c> or <c>pwsh.exe</c>.</param>
/// <param name="ExecutablePath">The full executable path, when readable.</param>
internal readonly record struct ProcessEntry(int ParentId, string Name, string? ExecutablePath);

/// <summary>
/// Reads one process's parent and name. .NET has no API for a process's
/// parent, so each platform reads it natively.
/// </summary>
internal interface IProcessTree
{
	/// <returns>The process, or null when it is gone or unreadable.</returns>
	ProcessEntry? Get(int processId);
}

internal static class ProcessTree
{
	public static IProcessTree ForCurrentPlatform()
	{
		if (OperatingSystem.IsLinux())
		{
			return new LinuxProcessTree();
		}

		if (OperatingSystem.IsMacOS())
		{
			return new MacProcessTree();
		}

		if (OperatingSystem.IsWindows())
		{
			return new WindowsProcessTree();
		}

		return new EmptyProcessTree();
	}

	private sealed class EmptyProcessTree : IProcessTree
	{
		public ProcessEntry? Get(int processId) => null;
	}
}

/// <summary>Reads <c>/proc/&lt;pid&gt;/{stat,comm,exe}</c>.</summary>
internal sealed class LinuxProcessTree : IProcessTree
{
	public ProcessEntry? Get(int processId)
	{
		try
		{
			string processDirectory = $"/proc/{processId}";
			string stat = File.ReadAllText($"{processDirectory}/stat");
			string name = File.ReadAllText($"{processDirectory}/comm").Trim();

			return new ProcessEntry(
				ParseParentId(stat),
				name,
				TryReadExecutable(processDirectory));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
		{
			return null;
		}
	}

	/// <summary>
	/// Parses the parent id from <c>/proc/&lt;pid&gt;/stat</c>, i.e.
	/// <c>pid (comm) state ppid ...</c>. The comm may itself contain spaces
	/// and parentheses, so fields are counted from the last <c>)</c>.
	/// </summary>
	internal static int ParseParentId(string stat)
	{
		int commEnd = stat.LastIndexOf(')');
		string[] fields = stat[(commEnd + 1)..].Split(
			' ',
			StringSplitOptions.RemoveEmptyEntries);

		return int.Parse(fields[1]);
	}

	private static string? TryReadExecutable(string processDirectory)
	{
		try
		{
			return new FileInfo($"{processDirectory}/exe").LinkTarget;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// Another user's process (e.g. under sudo): the name still identifies it.
			return null;
		}
	}
}

/// <summary>Reads process info through libproc.</summary>
internal sealed partial class MacProcessTree : IProcessTree
{
	private const string LIB_PROC = "/usr/lib/libproc.dylib";

	// proc_bsdinfo: twelve 32-bit fields (pbi_ppid is the fifth), then
	// pbi_comm[16] and pbi_name[32], then six more 32-bit fields and two 64-bit.
	private const int PROC_PIDTBSDINFO = 3;
	private const int BSD_INFO_SIZE = 136;
	private const int PARENT_ID_OFFSET = 16;
	private const int COMM_OFFSET = 48;
	private const int COMM_LENGTH = 16;
	private const int PATH_MAX_LENGTH = 4096;

	public ProcessEntry? Get(int processId)
	{
		try
		{
			var bsdInfo = new byte[BSD_INFO_SIZE];
			if (ProcPidInfo(processId, PROC_PIDTBSDINFO, 0, bsdInfo, BSD_INFO_SIZE) != BSD_INFO_SIZE)
			{
				return null;
			}

			int parentId = BitConverter.ToInt32(bsdInfo, PARENT_ID_OFFSET);
			string comm = ReadCString(bsdInfo.AsSpan(COMM_OFFSET, COMM_LENGTH));

			var pathBuffer = new byte[PATH_MAX_LENGTH];
			int pathLength = ProcPidPath(processId, pathBuffer, PATH_MAX_LENGTH);
			string? path = pathLength > 0
				? Encoding.UTF8.GetString(pathBuffer, 0, pathLength)
				: null;

			return new ProcessEntry(parentId, path is null ? comm : Path.GetFileName(path), path);
		}
		catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
		{
			return null;
		}
	}

	private static string ReadCString(ReadOnlySpan<byte> bytes)
	{
		int terminator = bytes.IndexOf((byte)0);

		return Encoding.UTF8.GetString(terminator < 0 ? bytes : bytes[..terminator]);
	}

	[LibraryImport(LIB_PROC, EntryPoint = "proc_pidinfo")]
	private static partial int ProcPidInfo(
		int processId,
		int flavor,
		ulong argument,
		[Out] byte[] buffer,
		int bufferSize);

	[LibraryImport(LIB_PROC, EntryPoint = "proc_pidpath")]
	private static partial int ProcPidPath(int processId, [Out] byte[] buffer, uint bufferSize);
}

/// <summary>
/// Reads the parent id through <c>NtQueryInformationProcess</c> and the image
/// path through <c>QueryFullProcessImageNameW</c>, both of which need only
/// limited query access.
/// </summary>
internal sealed partial class WindowsProcessTree : IProcessTree
{
	private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
	private const int PROCESS_BASIC_INFORMATION_CLASS = 0;
	private const int MAX_PATH_LENGTH = 32768;

	public ProcessEntry? Get(int processId)
	{
		try
		{
			nint handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)processId);
			if (handle == 0)
			{
				return null;
			}

			try
			{
				var basicInfo = new ProcessBasicInformation();
				if (NtQueryInformationProcess(
						handle,
						PROCESS_BASIC_INFORMATION_CLASS,
						ref basicInfo,
						Marshal.SizeOf<ProcessBasicInformation>(),
						out _)
					!= 0)
				{
					return null;
				}

				var pathBuffer = new ushort[MAX_PATH_LENGTH];
				uint pathLength = MAX_PATH_LENGTH;
				string? path = QueryFullProcessImageName(handle, 0, pathBuffer, ref pathLength)
					? new string(MemoryMarshal.Cast<ushort, char>(pathBuffer.AsSpan(0, (int)pathLength)))
					: null;
				if (path is null)
				{
					return null;
				}

				return new ProcessEntry(
					(int)basicInfo.InheritedFromUniqueProcessId,
					Path.GetFileName(path.Replace('\\', '/')),
					path);
			}
			finally
			{
				CloseHandle(handle);
			}
		}
		catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
		{
			return null;
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct ProcessBasicInformation
	{
		public nint ExitStatus;
		public nint PebBaseAddress;
		public nint AffinityMask;
		public nint BasePriority;
		public nint UniqueProcessId;
		public nint InheritedFromUniqueProcessId;
	}

	[LibraryImport("kernel32.dll", SetLastError = true)]
	private static partial nint OpenProcess(
		uint desiredAccess,
		[MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
		uint processId);

	[LibraryImport("kernel32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool CloseHandle(nint handle);

	[LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool QueryFullProcessImageName(
		nint process,
		uint flags,
		[Out] ushort[] exeName,
		ref uint size);

	[LibraryImport("ntdll.dll")]
	private static partial int NtQueryInformationProcess(
		nint process,
		int processInformationClass,
		ref ProcessBasicInformation processInformation,
		int processInformationLength,
		out int returnLength);
}
