using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using JDMallen.Shelly.Tests.Fakes;
using Xunit;

namespace JDMallen.Shelly.Tests;

public partial class StringsTests
{
	private static readonly string[] TranslatedCultureNames =
	[
		"zh-Hans", "es", "pt-BR", "de", "ja", "fr", "ru", "ko", "id", "it",
		"pl", "tr", "uk", "vi", "zh-Hant", "nl", "cs", "sv", "th",
	];

	public static TheoryData<string> TranslatedCultures => new(TranslatedCultureNames);

	public static TheoryData<string, char> MenuLabels => new()
	{
		{ nameof(Strings.MenuExecute), 'x' },
		{ nameof(Strings.MenuEdit), 'e' },
		{ nameof(Strings.MenuExplain), 'p' },
		{ nameof(Strings.MenuCopy), 'c' },
		{ nameof(Strings.MenuRetry), 'r' },
		{ nameof(Strings.MenuQuit), 'q' },
	};

	[Theory]
	[MemberData(nameof(TranslatedCultures))]
	public void Culture_TranslatesEveryKeyWithMatchingPlaceholders(string cultureName)
	{
		ResourceSet ownStrings = OwnResourceSet(CultureInfo.GetCultureInfo(cultureName));

		foreach ((string key, string neutralValue) in NeutralStrings())
		{
			string? translated = ownStrings.GetString(key);
			Assert.False(string.IsNullOrWhiteSpace(translated), $"{cultureName}: {key}");
			Assert.Equal(Placeholders(neutralValue), Placeholders(translated));
		}
	}

	[Fact]
	public void Neutral_ResponseLanguageIsEmpty()
	{
		Assert.Equal(string.Empty, Strings.ResponseLanguage(CultureInfo.InvariantCulture));
	}

	[Theory]
	[MemberData(nameof(MenuLabels))]
	public void MenuLabel_KeepsItsKeyInEveryCulture(string key, char hotkey)
	{
		Assert.Contains(
			$"[{hotkey}]",
			Strings.Get(key, CultureInfo.InvariantCulture));
		foreach (string cultureName in TranslatedCultureNames)
		{
			Assert.Contains(
				$"[{hotkey}]",
				Strings.Get(key, CultureInfo.GetCultureInfo(cultureName)));
		}
	}

	[Theory]
	[InlineData("", "e[x]ecute  [e]dit  ex[p]lain  [c]opy  [r]etry  [q]uit")]
	[InlineData(
		"de",
		"[x] ausführen  [e] bearbeiten  [p] erklären  [c] kopieren  [r] wiederholen  [q] beenden")]
	[InlineData("ja-JP", "[x] 実行  [e] 編集  [p] 説明  [c] コピー  [r] 再試行  [q] 終了")]
	public async Task ReplLoop_RendersMenuInCurrentUICulture(
		string cultureName,
		string expectedMenu)
	{
		CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
		var provider = new FakeChatProvider();
		var io = new FakeConsoleIO();
		var clipboard = new FakeClipboard();
		provider.SuggestionResponses.Enqueue("ls -la");
		io.ReadKeys.Enqueue(ConsoleKey.Q);

		await new ReplLoop(
				provider,
				io,
				clipboard,
				editEnabled: true)
			.RunAsync("list files", TestContext.Current.CancellationToken);

		Assert.Contains(expectedMenu, io.Output.ToString());
	}

	private static ResourceSet OwnResourceSet(CultureInfo culture) =>
		Strings.ResourceManager.GetResourceSet(
			culture,
			createIfNotExists: true,
			tryParents: false)
		?? throw new InvalidOperationException($"No resources for {culture.Name}.");

	private static IEnumerable<(string Key, string Value)> NeutralStrings() =>
		OwnResourceSet(CultureInfo.InvariantCulture)
			.Cast<DictionaryEntry>()
			.Select(entry => ((string)entry.Key, (string)entry.Value!))
			.OrderBy(entry => entry.Item1, StringComparer.Ordinal);

	private static string[] Placeholders(string value) =>
		PlaceholderPattern()
			.Matches(value)
			.Select(match => match.Value)
			.Order(StringComparer.Ordinal)
			.ToArray();

	[GeneratedRegex(@"\{\d+\}")]
	private static partial Regex PlaceholderPattern();
}
