using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Platform.Windows;

namespace RobloxAntiAfk.Core.Tests;

/// <summary>
/// Проверяет, что вызовы системы на Windows объявлены верно. Только чтение:
/// ничего не кликает, не переключает окна и не ставит перехват мыши.
/// </summary>
public class WindowsNativeSmokeTests
{
    private static void RequireWindows() => Assert.SkipUnless(OperatingSystem.IsWindows(), "Проверка только для Windows");

    [Fact]
    public void Reads_windows_without_crashing()
    {
        RequireWindows();
        var windows = new WindowsWindowManager(new WindowsInputSimulator());

        var roblox = windows.FindRobloxWindows();
        var foreground = windows.GetForegroundWindow();

        Assert.All(roblox, window => Assert.NotEqual(WindowHandle.None, window));
        _ = windows.IsMinimized(foreground);
        _ = windows.GetBounds(foreground);
        Assert.Null(windows.GetBounds(new WindowHandle(-1)));
    }

    [Fact]
    public void Reads_cursor_position_without_moving_it()
    {
        RequireWindows();

        var cursor = new WindowsInputSimulator().GetCursor();

        Assert.False(double.IsNaN(cursor.X));
    }

    [Fact]
    public void Reads_idle_time()
    {
        RequireWindows();

        Assert.True(WindowsPlatform.Create().Idle.TimeSinceLastInput >= TimeSpan.Zero);
    }
}
