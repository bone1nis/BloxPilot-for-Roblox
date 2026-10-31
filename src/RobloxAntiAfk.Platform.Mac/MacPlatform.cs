using System.Diagnostics;
using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Platform.Mac.Native;

namespace RobloxAntiAfk.Platform.Mac;

public static class MacPlatform
{
    public static PlatformServices Create()
    {
        ObjC.EnsureAppKitLoaded();
        return new PlatformServices(
            new MacWindowManager(),
            new MacInputSimulator(),
            new MacIdleMonitor(),
            new MacSleepBlocker(),
            new MacInputPermission(),
            new MacMouseCapture(),
            // Если Roblox на весь экран, macOS показывает анимацию перехода.
            WindowSettleDelay: TimeSpan.FromSeconds(1));
    }

    private sealed class MacIdleMonitor : IIdleMonitor
    {
        public TimeSpan TimeSinceLastInput => TimeSpan.FromSeconds(
            CoreGraphics.CGEventSourceSecondsSinceLastEventType(CoreGraphics.CombinedSessionState, CoreGraphics.AnyInputEvent));
    }

    private sealed class MacInputPermission : IInputPermission
    {
        private const string SettingsUrl = "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility";

        public bool IsGranted => Accessibility.AXIsProcessTrusted() != 0;

        public void Prompt()
        {
            var key = CoreFoundation.CreateString("AXTrustedCheckOptionPrompt");
            var value = CoreFoundation.BooleanTrue;
            var options = CoreFoundation.CFDictionaryCreate(
                0, ref key, ref value, 1, CoreFoundation.DictionaryKeyCallBacks, CoreFoundation.DictionaryValueCallBacks);
            Accessibility.AXIsProcessTrustedWithOptions(options);
            CoreFoundation.CFRelease(options);
            CoreFoundation.CFRelease(key);
        }

        public void OpenSystemSettings() => Process.Start("open", SettingsUrl)?.Dispose();
    }

    /// <summary>Активность нужна и без запрета сна: иначе macOS притормозит таймеры приложения в фоне.</summary>
    private sealed class MacSleepBlocker : ISleepBlocker
    {
        private const ulong IdleSystemSleepDisabled = 1UL << 20;
        private const ulong UserInitiated = 0x00FFFFFFUL | IdleSystemSleepDisabled;
        private const ulong UserInitiatedAllowingSleep = UserInitiated & ~IdleSystemSleepDisabled;
        private const ulong IdleDisplaySleepDisabled = 1UL << 40;

        private nint _activity;

        public void Apply(SleepMode mode)
        {
            var pool = ObjC.objc_autoreleasePoolPush();
            try
            {
                var processInfo = ObjC.Send(ObjC.Class("NSProcessInfo"), "processInfo");
                if (_activity != 0)
                {
                    ObjC.Send(processInfo, "endActivity:", _activity);
                    ObjC.Send(_activity, "release");
                    _activity = 0;
                }

                if (mode == SleepMode.Allow)
                {
                    return;
                }

                var options = mode == SleepMode.KeepAwake ? UserInitiated | IdleDisplaySleepDisabled : UserInitiatedAllowingSleep;
                var reason = CoreFoundation.CreateString("Roblox AntiAFK");
                _activity = ObjC.Send(ObjC.BeginActivity(processInfo, options, reason), "retain");
                CoreFoundation.CFRelease(reason);
            }
            finally
            {
                ObjC.objc_autoreleasePoolPop(pool);
            }
        }
    }
}
