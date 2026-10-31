namespace RobloxAntiAfk.Core.Domain;

/// <summary>Область в долях окна: не съезжает, если окно подвинуть или изменить его размер.</summary>
public sealed record WindowArea(double X, double Y, double Width, double Height)
{
    public static WindowArea FromScreen(ScreenRect area, ScreenRect window)
    {
        var left = Math.Clamp((area.X - window.X) / window.Width, 0, 1);
        var top = Math.Clamp((area.Y - window.Y) / window.Height, 0, 1);
        var right = Math.Clamp((area.X + area.Width - window.X) / window.Width, 0, 1);
        var bottom = Math.Clamp((area.Y + area.Height - window.Y) / window.Height, 0, 1);
        return new WindowArea(left, top, right - left, bottom - top);
    }

    public ScreenRect ToScreen(ScreenRect window) => new(
        window.X + window.Width * X,
        window.Y + window.Height * Y,
        window.Width * Width,
        window.Height * Height);

    public WindowArea Clamped()
    {
        var x = Math.Clamp(X, 0, 1);
        var y = Math.Clamp(Y, 0, 1);
        return new WindowArea(x, y, Math.Clamp(Width, 0, 1 - x), Math.Clamp(Height, 0, 1 - y));
    }
}

/// <summary>Строка текста на снимке. Координаты в долях снимка, отсчёт от левого верхнего угла.</summary>
public readonly record struct RecognizedLine(string Text, double X, double Y, double Width, double Height);

/// <summary>Снимок: четыре байта на пиксель в порядке синий, зелёный, красный, прозрачность.</summary>
public sealed record PixelImage(int Width, int Height, byte[] Bgra);

public enum TextReadStatus
{
    Success,
    PermissionMissing,

    /// <summary>Снимок одного цвета: так бывает, если система не отдаёт изображение окна.</summary>
    EmptyCapture,
    Failed,
}

public sealed record TextReadResult(TextReadStatus Status, IReadOnlyList<RecognizedLine> Lines, string Detail = "")
{
    public static TextReadResult Of(TextReadStatus status, string detail = "") => new(status, [], detail);
}

public enum TranslationStatus
{
    Disabled,
    NeedArea,
    Ready,
    SelectingArea,
    Reading,
    Shown,
    ShownOriginal,
    NoText,
    AreaTooSmall,
    RobloxMissing,
    RobloxNotInFront,
    InputPermissionMissing,
    ScreenPermissionMissing,
    EmptyCapture,
    SelectionFailed,
    ReadFailed,
}

public sealed record TranslationSnapshot(
    TranslationStatus Status,
    bool IsSelecting,
    bool IsOverlayActive,
    bool IsHotkeyActive,
    string HotkeyLabel,
    bool IsScreenPermissionGranted,
    string SourceText,
    string TranslatedText,
    string Detail);
