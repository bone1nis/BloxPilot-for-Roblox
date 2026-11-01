using System.Globalization;
using RobloxAntiAfk.Core.Domain;

namespace RobloxAntiAfk.Core.Localization;

/// <summary>Тексты интерфейса. Русский текст служит ключом, поэтому код остаётся читаемым, а без перевода показывается оригинал.</summary>
public static class L
{
    public static AppLanguage Current { get; set; } = AppLanguage.Russian;

    public static string T(string russian) => Translate(russian, Current);

    public static string F(string russianFormat, params object?[] arguments) =>
        string.Format(CultureInfo.InvariantCulture, T(russianFormat), arguments);

    public static string Translate(string russian, AppLanguage language) =>
        language == AppLanguage.English && EnglishTexts.All.TryGetValue(russian, out var english) ? english : russian;

    internal static IEnumerable<string> EnglishKeys => EnglishTexts.All.Keys;
}
