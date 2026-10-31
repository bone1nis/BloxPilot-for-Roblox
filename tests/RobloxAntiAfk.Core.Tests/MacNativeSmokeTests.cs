using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Platform.Mac;

namespace RobloxAntiAfk.Core.Tests;

/// <summary>
/// Проверяет, что вызовы системы на Mac объявлены верно. Только чтение:
/// ничего не кликает, не переключает окна и не показывает запрос разрешения.
/// </summary>
public class MacNativeSmokeTests
{
    private static void RequireMac() => Assert.SkipUnless(OperatingSystem.IsMacOS(), "Проверка только для macOS");

    [Fact]
    public void Reads_windows_without_crashing()
    {
        RequireMac();
        var windows = new MacWindowManager();

        var roblox = windows.FindRobloxWindows();
        var foreground = windows.GetForegroundWindow();

        Assert.All(roblox, window => Assert.True(window.Value > 0));
        Assert.True(foreground.Value >= 0);
        _ = windows.GetBounds(foreground);
        Assert.Null(windows.GetBounds(new WindowHandle(int.MaxValue)));
    }

    [Fact]
    public void Reads_cursor_position_without_moving_it()
    {
        RequireMac();

        var cursor = new MacInputSimulator().GetCursor();

        Assert.False(double.IsNaN(cursor.X));
    }

    [Fact]
    public void Reads_idle_time_and_permission_state()
    {
        RequireMac();
        var platform = MacPlatform.Create();

        Assert.True(platform.Idle.TimeSinceLastInput >= TimeSpan.Zero);
        _ = platform.Permission.IsGranted;
    }
}
