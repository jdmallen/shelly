using Xunit;

namespace JDMallen.Shelly.Tests;

public sealed class AzureCliTests : IDisposable
{
	private readonly string _directory =
		Directory.CreateTempSubdirectory("shelly-azcli-").FullName;

	public void Dispose() => Directory.Delete(_directory, true);

	private string CreateBinDirectory(string name, params string[] files)
	{
		string directory =
			Directory.CreateDirectory(Path.Combine(_directory, name)).FullName;
		foreach (string file in files)
		{
			File.WriteAllText(Path.Combine(directory, file), string.Empty);
		}

		return directory;
	}

	[Fact]
	public void Find_Unix_ReturnsFirstAzOnPath()
	{
		string empty = CreateBinDirectory("empty");
		string first = CreateBinDirectory("first", "az");
		string second = CreateBinDirectory("second", "az");
		string path = string.Join(
			Path.PathSeparator,
			empty,
			first,
			second);

		Assert.Equal(
			Path.Combine(first, "az"),
			AzureCli.Find(name => name == "PATH" ? path : null, false));
	}

	[Fact]
	public void Find_Windows_PrefersAzCmd()
	{
		string directory = CreateBinDirectory("win", "az", "az.cmd");

		Assert.Equal(
			Path.Combine(directory, "az.cmd"),
			AzureCli.Find(name => name == "PATH" ? directory : null, true));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void Find_NoPath_ReturnsNull(string? path) =>
		Assert.Null(AzureCli.Find(_ => path, false));

	[Fact]
	public void Find_NotOnPath_ReturnsNull()
	{
		string directory = CreateBinDirectory("other", "python3");

		Assert.Null(AzureCli.Find(_ => directory, false));
	}

	[Theory]
	[InlineData("", "", "az login --scope https://ai.azure.com/.default")]
	[InlineData(
		" contoso.onmicrosoft.com ",
		"",
		"az login --tenant contoso.onmicrosoft.com --scope https://ai.azure.com/.default")]
	[InlineData(
		"",
		"https://cognitiveservices.azure.com/.default",
		"az login --scope https://cognitiveservices.azure.com/.default")]
	public void SignInCommand_UsesTenantAndScope(
		string tenantId,
		string tokenScope,
		string expected) => Assert.Equal(
		expected,
		AzureCli.SignInCommand(
			new AzureConfig
			{
				TenantId = tenantId,
				TokenScope = tokenScope
			}));
}
