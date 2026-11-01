using System.Text.RegularExpressions;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Localization;
using RobloxAntiAfk.Core.Services;
using RobloxAntiAfk.Core.Storage;

namespace RobloxAntiAfk.Core.Tests;

/// <summary>Сверяет исходники с переводами: новый текст без английской версии не пройдёт тесты.</summary>
public partial class LocalizationTests
{
    private static readonly string[] IgnoredFiles = ["EnglishTexts.cs", "SelfTest.cs"];

    private static string SourceFolder()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "RobloxAntiAfk.slnx")))
        {
            folder = folder.Parent;
        }

        return Path.Combine(folder!.FullName, "src");
    }

    private static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(SourceFolder(), pattern, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !IgnoredFiles.Contains(Path.GetFileName(path)));

    private static IEnumerable<string> UsedTexts()
    {
        foreach (var path in SourceFiles("*.cs"))
        {
            foreach (Match match in CodeText().Matches(File.ReadAllText(path)))
            {
                yield return match.Groups["text"].Value.Replace("\\n", "\n");
            }
        }

        foreach (var path in SourceFiles("*.axaml"))
        {
            foreach (Match match in MarkupText().Matches(File.ReadAllText(path)))
            {
                yield return match.Groups["text"].Value;
            }
        }
    }

    [Fact]
    public void Every_text_in_the_interface_has_an_english_version()
    {
        var missing = UsedTexts().Distinct().Where(text => !L.EnglishKeys.Contains(text)).ToArray();

        Assert.True(missing.Length == 0, "Нет перевода: " + string.Join(" | ", missing));
    }

    [Fact]
    public void There_are_no_unused_translations()
    {
        var used = UsedTexts().ToHashSet();

        var unused = L.EnglishKeys.Where(text => !used.Contains(text)).ToArray();

        Assert.True(unused.Length == 0, "Лишний перевод: " + string.Join(" | ", unused));
    }

    [Fact]
    public void Russian_text_in_code_is_not_left_outside_the_translator()
    {
        var plain = SourceFiles("*.cs")
            .SelectMany(path => File.ReadAllLines(path).Select(line => (path, line)))
            .Where(item => !item.line.TrimStart().StartsWith("//", StringComparison.Ordinal)
                && !item.line.Contains("throw new", StringComparison.Ordinal)
                && !item.line.Contains("Plural(", StringComparison.Ordinal))
            .Where(item => RussianLiteral().Matches(item.line)
                .Any(match => !item.line[..match.Index].EndsWith("L.T(", StringComparison.Ordinal)
                    && !item.line[..match.Index].EndsWith("L.F(", StringComparison.Ordinal)))
            .Select(item => $"{Path.GetFileName(item.path)}: {item.line.Trim()}")
            .ToArray();

        Assert.True(plain.Length == 0, string.Join("\n", plain));
    }

    [Fact]
    public void Russian_text_in_markup_is_not_left_outside_the_translator()
    {
        var plain = SourceFiles("*.axaml")
            .SelectMany(path => RussianAttribute().Matches(File.ReadAllText(path)).Select(match => match.Value))
            .Where(attribute => !attribute.Contains("{l:T '", StringComparison.Ordinal))
            .ToArray();

        Assert.True(plain.Length == 0, string.Join("\n", plain));
    }

    [Fact]
    public void Translations_keep_their_placeholders_and_are_really_english()
    {
        foreach (var russian in L.EnglishKeys)
        {
            var english = L.Translate(russian, AppLanguage.English);

            Assert.Equal(Placeholders(russian), Placeholders(english));
            if (russian is not ("Русский" or "Язык / Language"))
            {
                // Адрес посредника отвечает по-русски, поэтому в подсказке его ответ приведён как есть.
                Assert.False(Cyrillic().IsMatch(english.Replace("Посредник работает", "")), english);
            }
        }
    }

    [Fact]
    public void Russian_stays_as_written_and_unknown_text_falls_back_to_it()
    {
        Assert.Equal("Настройки", L.Translate("Настройки", AppLanguage.Russian));
        Assert.Equal("Settings", L.Translate("Настройки", AppLanguage.English));
        Assert.Equal("Такого нет", L.Translate("Такого нет", AppLanguage.English));
    }

    [Fact]
    public void Language_and_theme_survive_saving_and_settings_reset()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var settings = new AppSettings { Language = AppLanguage.English, Theme = AppTheme.Light };
            new JsonFileStore<AppSettings>(path, () => new AppSettings()).Save(settings);

            var loaded = new JsonFileStore<AppSettings>(path, () => new AppSettings()).Load();

            Assert.Equal(AppLanguage.English, loaded.Language);
            Assert.Equal(AppTheme.Light, loaded.Theme);
            Assert.Equal(AppLanguage.English, loaded.ToDefaultsKeepingUserData().Language);
            Assert.Equal(AppTheme.Light, loaded.ToDefaultsKeepingUserData().Theme);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Existing_users_stay_on_russian_and_the_dark_theme()
    {
        Assert.Equal(AppLanguage.Russian, new AppSettings().Language);
        Assert.Equal(AppTheme.Dark, new AppSettings().Theme);
    }

    [Fact]
    public void Both_themes_define_the_same_colors()
    {
        var theme = File.ReadAllText(Path.Combine(SourceFolder(), "RobloxAntiAfk.App", "Styles", "Theme.axaml"));
        var dark = theme[theme.IndexOf("x:Key=\"Dark\"", StringComparison.Ordinal)..theme.IndexOf("x:Key=\"Light\"", StringComparison.Ordinal)];
        var light = theme[theme.IndexOf("x:Key=\"Light\"", StringComparison.Ordinal)..];

        Assert.NotEmpty(BrushKeys(dark));
        Assert.Equal(BrushKeys(dark), BrushKeys(light));
    }

    private static string[] BrushKeys(string text) =>
        BrushKey().Matches(text).Select(match => match.Groups["key"].Value).Order(StringComparer.Ordinal).ToArray();

    private static string[] Placeholders(string text) =>
        Placeholder().Matches(text).Select(match => match.Value).Order(StringComparer.Ordinal).ToArray();

    [GeneratedRegex("""L\.[TF]\("(?<text>(?:[^"\\]|\\.)*)"[,)]""")]
    private static partial Regex CodeText();

    [GeneratedRegex("""\{l:T '(?<text>[^']*)'\}""")]
    private static partial Regex MarkupText();

    [GeneratedRegex("""\$?"(?:[^"\\]|\\.)*[А-Яа-яЁё](?:[^"\\]|\\.)*"(?!\s*\])""")]
    private static partial Regex RussianLiteral();

    [GeneratedRegex("""[A-Za-z.:]+="[^"]*[А-Яа-яЁё][^"]*"(?=[\s/>])""")]
    private static partial Regex RussianAttribute();

    [GeneratedRegex(@"\{\d+[^}]*\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex("[А-Яа-яЁё]")]
    private static partial Regex Cyrillic();

    [GeneratedRegex("""<SolidColorBrush x:Key="(?<key>\w+)">""")]
    private static partial Regex BrushKey();
}
