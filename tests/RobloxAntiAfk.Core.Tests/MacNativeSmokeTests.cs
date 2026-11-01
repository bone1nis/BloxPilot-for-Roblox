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

    /// <summary>Сочетание здесь не регистрируется: проверяем только, что нужные функции есть в системе.</summary>
    [Theory]
    [InlineData(RobloxAntiAfk.Platform.Mac.Native.Carbon.Library, "GetApplicationEventTarget")]
    [InlineData(RobloxAntiAfk.Platform.Mac.Native.Carbon.Library, "InstallEventHandler")]
    [InlineData(RobloxAntiAfk.Platform.Mac.Native.Carbon.Library, "RemoveEventHandler")]
    [InlineData(RobloxAntiAfk.Platform.Mac.Native.Carbon.Library, "RegisterEventHotKey")]
    [InlineData(RobloxAntiAfk.Platform.Mac.Native.Carbon.Library, "UnregisterEventHotKey")]
    [InlineData(RobloxAntiAfk.Platform.Mac.Native.Carbon.Library, "GetEventParameter")]
    [InlineData("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics", "CGPreflightScreenCaptureAccess")]
    [InlineData("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics", "CGRequestScreenCaptureAccess")]
    [InlineData("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics", "CGEventGetFlags")]
    [InlineData("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics", "CGEventGetIntegerValueField")]
    public void System_exports_new_native_functions(string library, string function)
    {
        RequireMac();

        Assert.NotEqual(0, System.Runtime.InteropServices.NativeLibrary.GetExport(System.Runtime.InteropServices.NativeLibrary.Load(library), function));
    }

    /// <summary>Спрашиваем про собственное приложение: ничего не скрывается и не показывается.</summary>
    [Fact]
    public void Reads_hidden_state_of_own_process()
    {
        RequireMac();

        var isHidden = new MacWindowManager().IsMinimized(new WindowHandle(Environment.ProcessId));

        Assert.False(isHidden);
    }

    [Fact]
    public void Reads_cursor_position_without_moving_it()
    {
        RequireMac();

        var cursor = new MacInputSimulator().GetCursor();

        Assert.False(double.IsNaN(cursor.X));
    }

    /// <summary>Запрос разрешения не показывается: это только проверка состояния.</summary>
    [Fact]
    public void Reads_screen_permission_state_and_hotkey_labels_differ()
    {
        RequireMac();
        var platform = MacPlatform.Create();

        _ = platform.TextReader.IsPermissionGranted;
        Assert.NotEqual(platform.Hotkey.Label, platform.TranslationHotkey.Label);
    }

    /// <summary>Раньше работало только первое сочетание: система отклоняла повторную установку того же обработчика.</summary>
    [Fact]
    public void Several_hotkeys_register_at_once_and_can_be_rebound()
    {
        RequireMac();
        var first = new MacHotkey(101, new Hotkey("F19", Control: true, Shift: true, Alt: true));
        var second = new MacHotkey(102, new Hotkey("F18", Control: true, Shift: true, Alt: true));

        try
        {
            Assert.True(first.Start(() => { }));
            Assert.True(second.Start(() => { }));

            second.Bind(new Hotkey("F17", Control: true, Shift: true, Alt: true));
            Assert.True(second.Start(() => { }));
            Assert.Equal("Control+Option+Shift+F17", second.Label);

            second.Bind(new Hotkey("НетТакой"));
            Assert.False(second.Start(() => { }));
        }
        finally
        {
            first.Stop();
            second.Stop();
        }
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
