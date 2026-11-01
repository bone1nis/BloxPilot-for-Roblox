using System.Text.Json.Serialization;
using RobloxAntiAfk.Core.Localization;

namespace RobloxAntiAfk.Core.Domain;

/// <summary>
/// Клавиша хранится по месту на клавиатуре, а не по букве, поэтому сочетание не зависит от раскладки.
/// Имена клавиш такие же, как у PhysicalKey в Avalonia: T, Digit1, F8, Space.
/// </summary>
public sealed record Hotkey(string Key, bool Control = false, bool Shift = false, bool Alt = false, bool Meta = false)
{
    public const string EscapeKey = "Escape";

    [JsonIgnore]
    public bool HasModifiers => Control || Shift || Alt || Meta;

    public string ToLabel(bool isMac)
    {
        List<string> parts = [];
        if (Control)
        {
            parts.Add(isMac ? "Control" : "Ctrl");
        }

        if (Alt)
        {
            parts.Add(isMac ? "Option" : "Alt");
        }

        if (Shift)
        {
            parts.Add("Shift");
        }

        if (Meta)
        {
            parts.Add(isMac ? "Command" : "Win");
        }

        parts.Add(KeyLabel(Key));
        return string.Join("+", parts);
    }

    private static string KeyLabel(string key) => key switch
    {
        EscapeKey => "Esc",
        "Space" => L.T("Пробел"),
        "ArrowUp" => L.T("Стрелка вверх"),
        "ArrowDown" => L.T("Стрелка вниз"),
        "ArrowLeft" => L.T("Стрелка влево"),
        "ArrowRight" => L.T("Стрелка вправо"),
        "Backquote" => "`",
        "Backslash" => "\\",
        "BracketLeft" => "[",
        "BracketRight" => "]",
        "Comma" => ",",
        "Equal" => "=",
        "Minus" => "-",
        "Period" => ".",
        "Quote" => "'",
        "Semicolon" => ";",
        "Slash" => "/",
        _ when key.StartsWith("Digit", StringComparison.Ordinal) => key["Digit".Length..],
        _ when key.StartsWith("NumPad", StringComparison.Ordinal) => "Num " + key["NumPad".Length..],
        _ => key,
    };
}
