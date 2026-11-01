using System.Diagnostics;
using System.Runtime.Versioning;
using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;

namespace RobloxAntiAfk.Platform.Windows;

public static class WindowsPlatform
{
    [SupportedOSPlatform("windows")]
    public static PlatformServices Create(ITextRecognizer? recognizer = null)
    {
        var input = new WindowsInputSimulator();
        return new PlatformServices(
            new WindowsWindowManager(input),
            input,
            new WindowsIdleMonitor(),
            new WindowsSleepBlocker(),
            new NoPermissionRequired(),
            new WindowsMouseCapture(),
            new WindowsInputRecorder(),
            new WindowsHotkey(1, new Hotkey("F8", Control: true, Shift: true)),
            new WindowsAutoStart(),
            new WindowsAudio(),
            new WindowsLauncher(),
            new RobloxLogWatcher(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "logs"),
                new SystemClock()),
            new WindowsHotkey(2, new Hotkey("X", Control: true, Shift: true)),
            new WindowsHotkey(3, new Hotkey(Hotkey.EscapeKey)),
            new WindowsScreenTextReader(recognizer),
            new WindowsOverlaySupport(),
            WindowSettleDelay: TimeSpan.FromMilliseconds(500));
    }

    private sealed class WindowsIdleMonitor : IIdleMonitor
    {
        public TimeSpan TimeSinceLastInput
        {
            get
            {
                var info = new LastInputInfo { Size = sizeof(uint) * 2 };
                NativeMethods.GetLastInputInfo(ref info);
                return TimeSpan.FromMilliseconds(unchecked(NativeMethods.GetTickCount() - info.Time));
            }
        }
    }

    /// <summary>Настройка действует на текущий поток: это поток интерфейса, он живёт всё время работы.</summary>
    private sealed class WindowsSleepBlocker : ISleepBlocker
    {
        public void Apply(SleepMode mode)
        {
            var flags = NativeMethods.ExecutionContinuous;
            if (mode == SleepMode.KeepAwake)
            {
                flags |= NativeMethods.ExecutionSystemRequired | NativeMethods.ExecutionDisplayRequired;
            }

            NativeMethods.SetThreadExecutionState(flags);
        }
    }

    private sealed class WindowsLauncher : IRobloxLauncher
    {
        public void Open(string launchUri) =>
            Process.Start(new ProcessStartInfo(launchUri) { UseShellExecute = true })?.Dispose();
    }

    private sealed class NoPermissionRequired : IInputPermission
    {
        public bool IsGranted => true;

        public void Prompt()
        {
        }

        public void OpenSystemSettings()
        {
        }
    }
}
