using RobloxAntiAfk.Core.Domain;

namespace RobloxAntiAfk.Core.Abstractions;

public interface IWindowManager
{
    IReadOnlyList<WindowHandle> FindRobloxWindows();

    WindowHandle GetForegroundWindow();

    /// <summary>Просит систему вывести окно вперёд. Результат проверяется через <see cref="GetForegroundWindow"/>.</summary>
    void RequestForeground(WindowHandle window);

    /// <summary>Границы окна или null, если его не видно на экране.</summary>
    ScreenRect? GetBounds(WindowHandle window);

    bool IsMinimized(WindowHandle window);

    void Restore(WindowHandle window);

    void Minimize(WindowHandle window);
}

public interface IInputSimulator
{
    ScreenPoint GetCursor();

    void MoveCursor(ScreenPoint point);

    void SendMouseButton(MouseButton button, bool isDown, ScreenPoint at);

    void SendKey(GameKey key, bool isDown);
}

public interface IIdleMonitor
{
    TimeSpan TimeSinceLastInput { get; }
}

public enum SleepMode
{
    /// <summary>Система ведёт себя как обычно.</summary>
    Allow,

    /// <summary>Система может уснуть, но не должна тормозить таймеры приложения в фоне.</summary>
    AllowButStayResponsive,

    /// <summary>Экран и компьютер не засыпают.</summary>
    KeepAwake,
}

public interface ISleepBlocker
{
    void Apply(SleepMode mode);
}

/// <summary>Разрешение на управление вводом. Требуется только на Mac.</summary>
public interface IInputPermission
{
    bool IsGranted { get; }

    void Prompt();

    void OpenSystemSettings();
}

/// <summary>Глобальный перехват левой кнопки мыши для выбора точки.</summary>
public interface IMouseCapture
{
    /// <summary>
    /// Начинает перехват. Если <paramref name="onMouseDown"/> вернул true, это нажатие и его отпускание
    /// не доходят до других приложений. Все остальные клики проходят как обычно.
    /// </summary>
    bool Start(Func<ScreenPoint, bool> onMouseDown);

    void Stop();
}

public sealed record PlatformServices(
    IWindowManager Windows,
    IInputSimulator Input,
    IIdleMonitor Idle,
    ISleepBlocker Sleep,
    IInputPermission Permission,
    IMouseCapture MouseCapture,
    TimeSpan WindowSettleDelay);
