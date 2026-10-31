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

    /// <summary>
    /// То же самое, но сообщает о движении с зажатой кнопкой и об отпускании.
    /// Движение проходит в другие приложения: без нажатия оно ничего в них не делает.
    /// </summary>
    bool StartDrag(Func<ScreenPoint, bool> onMouseDown, Action<ScreenPoint> onMouseMove, Action<ScreenPoint> onMouseUp);

    void Stop();
}

/// <summary>Слушает клавиатуру и мышь только на время записи макроса и ничего не блокирует.</summary>
public interface IInputRecorder
{
    bool Start(Action<RawInputEvent> onInput);

    void Stop();
}

public interface IGlobalHotkey
{
    string Label { get; }

    /// <summary>Обработчик вызывается в потоке интерфейса. Возвращает false, если сочетание уже занято.</summary>
    bool Start(Action onPressed);

    void Stop();
}

public interface IAutoStart
{
    void Apply(bool isEnabled);
}

public interface IRobloxAudio
{
    bool IsSupported { get; }

    void SetMuted(IReadOnlyList<WindowHandle> windows, bool isMuted);
}

public interface IRobloxLauncher
{
    /// <summary>Открывает адрес запуска, собранный <see cref="GameLink"/>.</summary>
    void Open(string launchUri);
}

public interface IDisconnectWatcher
{
    /// <summary>Запоминает текущее место в журнале: прошлые отключения не считаются.</summary>
    void Arm();

    /// <summary>True, если с прошлой проверки появилась запись об отключении от сервера.</summary>
    bool Poll();
}

/// <summary>Распознаёт текст на картинке. Вызывается не из потока интерфейса.</summary>
public interface ITextRecognizer
{
    IReadOnlyList<RecognizedLine> Recognize(PixelImage image, CancellationToken cancellation);
}

/// <summary>Читает текст с экрана: снимок сразу распознаётся и нигде не сохраняется.</summary>
public interface IScreenTextReader
{
    bool IsPermissionGranted { get; }

    void RequestPermission();

    void OpenPermissionSettings();

    Task<TextReadResult> ReadAsync(ScreenRect area, CancellationToken cancellation);
}

/// <summary>Настраивает окно подписи: клики идут сквозь него, оно не забирает фокус и видно поверх игры.</summary>
public interface IOverlaySupport
{
    /// <param name="handle">Системный идентификатор окна.</param>
    /// <param name="aboveFullScreen">Roblox занимает весь экран и подпись должна быть видна поверх него.</param>
    void Prepare(nint handle, bool aboveFullScreen);

    void Release();

    /// <summary>True, если клик в этой точке пройдёт мимо окна.</summary>
    bool IsClickThrough(nint handle, ScreenPoint point);
}

public sealed record PlatformServices(
    IWindowManager Windows,
    IInputSimulator Input,
    IIdleMonitor Idle,
    ISleepBlocker Sleep,
    IInputPermission Permission,
    IMouseCapture MouseCapture,
    IInputRecorder Recorder,
    IGlobalHotkey Hotkey,
    IAutoStart AutoStart,
    IRobloxAudio Audio,
    IRobloxLauncher Launcher,
    IDisconnectWatcher Disconnects,
    IGlobalHotkey TranslationHotkey,
    IScreenTextReader TextReader,
    IOverlaySupport Overlay,
    TimeSpan WindowSettleDelay);
