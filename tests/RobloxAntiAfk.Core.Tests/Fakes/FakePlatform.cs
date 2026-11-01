using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;
using RobloxAntiAfk.Core.Storage;

namespace RobloxAntiAfk.Core.Tests.Fakes;

/// <summary>Время идёт только в момент ожидания, поэтому тесты не ждут по-настоящему.</summary>
public sealed class FakeClock : IClock
{
    public DateTimeOffset Now { get; private set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public TimeSpan TotalDelayed { get; private set; }

    public Task Delay(TimeSpan duration)
    {
        Advance(duration);
        TotalDelayed += duration;
        return Task.CompletedTask;
    }

    public Task Yield() => Task.CompletedTask;

    public void Advance(TimeSpan duration) => Now += duration;
}

public sealed class FakeWindows : IWindowManager
{
    public List<WindowHandle> Roblox { get; } = [];
    public Dictionary<WindowHandle, ScreenRect> Bounds { get; } = [];
    public HashSet<WindowHandle> Minimized { get; } = [];
    public HashSet<WindowHandle> RefuseForeground { get; } = [];
    public List<string> Log { get; } = [];
    public WindowHandle Foreground { get; set; } = new(999);

    public IReadOnlyList<WindowHandle> FindRobloxWindows() => Roblox.ToArray();

    public WindowHandle GetForegroundWindow() => Foreground;

    public void RequestForeground(WindowHandle window)
    {
        Log.Add($"front:{window.Value}");
        if (!RefuseForeground.Contains(window))
        {
            Foreground = window;
        }
    }

    public ScreenRect? GetBounds(WindowHandle window) => Bounds.TryGetValue(window, out var rect) ? rect : null;

    public bool IsMinimized(WindowHandle window) => Minimized.Contains(window);

    public void Restore(WindowHandle window)
    {
        Log.Add($"restore:{window.Value}");
        Minimized.Remove(window);
    }

    public void Minimize(WindowHandle window)
    {
        Log.Add($"minimize:{window.Value}");
        Minimized.Add(window);
    }
}

public sealed class FakeInput : IInputSimulator
{
    public ScreenPoint Cursor { get; set; } = new(5, 5);
    public List<string> Events { get; } = [];

    public ScreenPoint GetCursor() => Cursor;

    public void MoveCursor(ScreenPoint point)
    {
        Cursor = point;
        Events.Add($"move:{point.X:0},{point.Y:0}");
    }

    public void SendMouseButton(MouseButton button, bool isDown, ScreenPoint at) =>
        Events.Add($"{button}:{(isDown ? "down" : "up")}:{at.X:0},{at.Y:0}");

    public void SendKey(GameKey key, bool isDown) => Events.Add($"{key}:{(isDown ? "down" : "up")}");
}

public sealed class FakeIdle : IIdleMonitor
{
    public TimeSpan TimeSinceLastInput { get; set; } = TimeSpan.FromHours(1);
}

public sealed class FakeSleep : ISleepBlocker
{
    public SleepMode Mode { get; private set; } = SleepMode.Allow;

    public void Apply(SleepMode mode) => Mode = mode;
}

public sealed class FakePermission : IInputPermission
{
    public bool IsGranted { get; set; } = true;
    public int Prompts { get; private set; }
    public int SettingsOpened { get; private set; }

    public void Prompt() => Prompts++;

    public void OpenSystemSettings() => SettingsOpened++;
}

public sealed class FakeCapture : IMouseCapture
{
    private Func<ScreenPoint, bool>? _handler;
    private Action<ScreenPoint>? _onMove;
    private Action<ScreenPoint>? _onUp;
    private Action? _onRightClick;
    private bool _isRightIncluded;

    public bool CanStart { get; set; } = true;
    public bool IsActive => _handler is not null;

    public bool Start(Func<ScreenPoint, bool> onMouseDown, bool includeRightButton = false)
    {
        if (!CanStart)
        {
            return false;
        }

        _handler = onMouseDown;
        _isRightIncluded = includeRightButton;
        return true;
    }

    public bool StartDrag(Func<ScreenPoint, bool> onMouseDown, Action<ScreenPoint> onMouseMove, Action<ScreenPoint> onMouseUp, Action onRightClick)
    {
        if (!Start(onMouseDown))
        {
            return false;
        }

        _onMove = onMouseMove;
        _onUp = onMouseUp;
        _onRightClick = onRightClick;
        return true;
    }

    public void Stop()
    {
        _handler = null;
        _onMove = null;
        _onUp = null;
        _onRightClick = null;
        _isRightIncluded = false;
    }

    public bool Click(ScreenPoint point) => _handler?.Invoke(point) ?? false;

    public bool RightClick(ScreenPoint point)
    {
        if (_onRightClick is { } onRightClick)
        {
            Stop();
            onRightClick();
            return true;
        }

        return _isRightIncluded && Click(point);
    }

    public bool Drag(ScreenPoint from, ScreenPoint to)
    {
        if (!Click(from))
        {
            return false;
        }

        _onMove?.Invoke(to);
        var onUp = _onUp;
        Stop();
        onUp?.Invoke(to);
        return true;
    }
}

public sealed class FakeRecorder : IInputRecorder
{
    private Action<RawInputEvent>? _handler;

    public bool CanStart { get; set; } = true;
    public bool IsActive => _handler is not null;

    public bool Start(Action<RawInputEvent> onInput)
    {
        if (!CanStart)
        {
            return false;
        }

        _handler = onInput;
        return true;
    }

    public void Stop() => _handler = null;

    public void Emit(RawInputEvent input) => _handler?.Invoke(input);
}

public sealed class FakeHotkey(string label = "Ctrl+Shift+F8") : IGlobalHotkey
{
    private Action? _handler;

    public bool CanStart { get; set; } = true;
    public bool IsStarted => _handler is not null;
    public string Label => label;

    public bool Start(Action onPressed)
    {
        if (!CanStart)
        {
            return false;
        }

        _handler = onPressed;
        return true;
    }

    public void Stop() => _handler = null;

    public void Press() => _handler?.Invoke();
}

public sealed class FakeTextReader : IScreenTextReader
{
    public bool IsPermissionGranted { get; set; } = true;
    public int PermissionRequests { get; private set; }
    public List<ScreenRect> Reads { get; } = [];
    public TextReadResult Result { get; set; } = new(TextReadStatus.Success, []);
    public SnapshotResult? CaptureResult { get; set; }
    public List<ScreenRect> Captures { get; } = [];
    public List<ScreenRect> ReadsFromSnapshot { get; } = [];

    public void RequestPermission() => PermissionRequests++;

    public void OpenPermissionSettings()
    {
    }

    public Task<TextReadResult> ReadAsync(ScreenRect area, CancellationToken cancellation)
    {
        Reads.Add(area);
        return Task.FromResult(Result);
    }

    public Task<SnapshotResult> CaptureAsync(ScreenRect area, CancellationToken cancellation)
    {
        Captures.Add(area);
        return Task.FromResult(CaptureResult ?? new SnapshotResult(TextReadStatus.Success, new ScreenSnapshot(area, null, null)));
    }

    public Task<TextReadResult> ReadAsync(ScreenSnapshot snapshot, ScreenRect area, CancellationToken cancellation)
    {
        ReadsFromSnapshot.Add(area);
        return Task.FromResult(Result);
    }
}

public sealed class FakeOverlay : ITranslationOverlay
{
    public ScreenRect? Selection { get; private set; }
    public ScreenRect? LastSelection { get; private set; }
    public ScreenSnapshot? Frozen { get; private set; }
    public string? Text { get; private set; }
    public bool IsError { get; private set; }
    public ScreenRect? Window { get; private set; }
    public int Hides { get; private set; }
    public ScreenRect PanelToReturn { get; set; } = new(0, 0, 1, 1);

    public void ShowFrozen(ScreenSnapshot snapshot, string prompt)
    {
        Frozen = snapshot;
        Text = prompt;
    }

    public void ShowSelection(ScreenRect area)
    {
        Selection = area;
        LastSelection = area;
    }

    public ScreenRect ShowText(ScreenRect area, ScreenRect window, string text, bool isError)
    {
        Text = text;
        IsError = isError;
        Window = window;
        return PanelToReturn;
    }

    public void Hide()
    {
        Hides++;
        Frozen = null;
        Selection = null;
        Text = null;
    }
}

public sealed class FakeOverlaySupport : IOverlaySupport
{
    public void Prepare(nint handle, bool aboveFullScreen)
    {
    }

    public void Release()
    {
    }

    public bool IsClickThrough(nint handle, ScreenPoint point) => true;
}

public sealed class FakeAutoStart : IAutoStart
{
    public List<bool> Applied { get; } = [];

    public void Apply(bool isEnabled) => Applied.Add(isEnabled);
}

public sealed class FakeAudio : IRobloxAudio
{
    public bool IsSupported { get; set; } = true;
    public List<bool> Calls { get; } = [];

    public void SetMuted(IReadOnlyList<WindowHandle> windows, bool isMuted) => Calls.Add(isMuted);
}

public sealed class FakeLauncher : IRobloxLauncher
{
    public List<string> Opened { get; } = [];

    public void Open(string launchUri) => Opened.Add(launchUri);
}

public sealed class FakeDisconnects : IDisconnectWatcher
{
    public int Arms { get; private set; }
    public bool IsDisconnected { get; set; }

    public void Arm() => Arms++;

    public bool Poll()
    {
        var result = IsDisconnected;
        IsDisconnected = false;
        return result;
    }
}

public sealed class MemoryStore<T>(T initial) : IDataStore<T>
{
    public T Value { get; private set; } = initial;
    public int Saves { get; private set; }

    public T Load() => Value;

    public void Save(T value)
    {
        Value = value;
        Saves++;
    }
}

/// <summary>Собранное окружение: подставные реализации всего, что зависит от системы.</summary>
public sealed class TestWorld
{
    public static readonly WindowHandle First = new(1);
    public static readonly WindowHandle Second = new(2);
    public static readonly WindowHandle OtherApp = new(999);

    public FakeClock Clock { get; } = new();
    public FakeWindows Windows { get; } = new();
    public FakeInput Input { get; } = new();
    public FakeIdle Idle { get; } = new();
    public FakeSleep Sleep { get; } = new();
    public FakePermission Permission { get; } = new();
    public FakeCapture Capture { get; } = new();
    public FakeRecorder Recorder { get; } = new();
    public FakeHotkey Hotkey { get; } = new();
    public FakeAutoStart AutoStart { get; } = new();
    public FakeAudio Audio { get; } = new();
    public FakeLauncher Launcher { get; } = new();
    public FakeDisconnects Disconnects { get; } = new();
    public FakeHotkey TranslationHotkey { get; } = new("Ctrl+Shift+X");
    public FakeHotkey CancelKey { get; } = new("Esc");
    public FakeTextReader TextReader { get; } = new();

    public PlatformServices Platform =>
        new(Windows, Input, Idle, Sleep, Permission, Capture, Recorder, Hotkey, AutoStart, Audio, Launcher, Disconnects,
            TranslationHotkey, CancelKey, TextReader, new FakeOverlaySupport(), TimeSpan.FromMilliseconds(500));

    public TestWorld WithRoblox(params WindowHandle[] windows)
    {
        foreach (var window in windows)
        {
            Windows.Roblox.Add(window);
            Windows.Bounds[window] = new ScreenRect(100, 200, 800, 600);
        }

        return this;
    }
}
