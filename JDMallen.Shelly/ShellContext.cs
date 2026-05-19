using System.Runtime.InteropServices;

namespace JDMallen.Shelly;

public static class ShellContext
{
    public static string Build()
    {
        var shellPath = Environment.GetEnvironmentVariable("SHELL");
        var shell = string.IsNullOrEmpty(shellPath) ? "unknown" : Path.GetFileName(shellPath);
        var os = RuntimeInformation.OSDescription;
        var pwd = Directory.GetCurrentDirectory();
        return $"Shell: {shell}, OS: {os}, PWD: {pwd}";
    }
}
