namespace RobloxAntiAfk.Core.Domain;

/// <summary>Непрозрачный идентификатор окна. На Windows это HWND, на Mac это PID приложения.</summary>
public readonly record struct WindowHandle(nint Value)
{
    public static readonly WindowHandle None = new(0);
}

public readonly record struct ScreenPoint(double X, double Y);

public readonly record struct ScreenRect(double X, double Y, double Width, double Height)
{
    public ScreenPoint Center => new(X + Width / 2, Y + Height / 2);

    public bool Contains(ScreenPoint point) =>
        point.X >= X && point.X < X + Width && point.Y >= Y && point.Y < Y + Height;
}

/// <summary>Место клика в долях окна: точка не съезжает, если окно подвинуть.</summary>
public sealed record WindowPoint(double X, double Y)
{
    public static WindowPoint FromScreen(ScreenPoint point, ScreenRect window) =>
        new WindowPoint((point.X - window.X) / window.Width, (point.Y - window.Y) / window.Height).Clamped();

    public ScreenPoint ToScreen(ScreenRect window) => new(window.X + window.Width * X, window.Y + window.Height * Y);

    public WindowPoint Clamped() => new(Math.Clamp(X, 0, 1), Math.Clamp(Y, 0, 1));
}
