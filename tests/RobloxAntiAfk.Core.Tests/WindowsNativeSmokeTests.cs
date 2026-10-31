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

    /// <summary>Снимок никуда не сохраняется. На машине без рабочего стола система может отказать, это не ошибка кода.</summary>
    [Fact]
    public void Screen_capture_returns_the_requested_size()
    {
        RequireWindows();

        try
        {
            var image = WindowsScreenTextReader.Capture(new ScreenRect(0, 0, 64, 32));

            Assert.Equal(64, image.Width);
            Assert.Equal(32, image.Height);
            Assert.Equal(64 * 32 * 4, image.Bgra.Length);
        }
        catch (InvalidOperationException exception)
        {
            Assert.Skip(exception.Message);
        }
    }

    [Fact]
    public void Single_color_picture_is_recognized_as_empty_capture()
    {
        byte[] flat = [1, 2, 3, 255, 1, 2, 3, 255];
        byte[] mixed = [1, 2, 3, 255, 9, 2, 3, 255];

        Assert.True(WindowsScreenTextReader.IsSingleColor(new PixelImage(2, 1, flat)));
        Assert.False(WindowsScreenTextReader.IsSingleColor(new PixelImage(2, 1, mixed)));
    }

    [Fact]
    public void Reads_idle_time()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Проверка только для Windows");
            return;
        }

        Assert.True(WindowsPlatform.Create().Idle.TimeSinceLastInput >= TimeSpan.Zero);
    }
}
