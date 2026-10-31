using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;
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

    public bool CanStart { get; set; } = true;
    public bool IsActive => _handler is not null;

    public bool Start(Func<ScreenPoint, bool> onMouseDown)
    {
        if (!CanStart)
        {
            return false;
        }

        _handler = onMouseDown;
        return true;
    }

    public void Stop() => _handler = null;

    public bool Click(ScreenPoint point) => _handler?.Invoke(point) ?? false;
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

    public PlatformServices Platform =>
        new(Windows, Input, Idle, Sleep, Permission, Capture, TimeSpan.FromMilliseconds(500));

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
