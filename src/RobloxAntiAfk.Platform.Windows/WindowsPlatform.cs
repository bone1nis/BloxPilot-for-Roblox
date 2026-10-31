using RobloxAntiAfk.Core.Abstractions;

namespace RobloxAntiAfk.Platform.Windows;

public static class WindowsPlatform
{
    public static PlatformServices Create()
    {
        var input = new WindowsInputSimulator();
        return new PlatformServices(
            new WindowsWindowManager(input),
            input,
            new WindowsIdleMonitor(),
            new WindowsSleepBlocker(),
            new NoPermissionRequired(),
            new WindowsMouseCapture(),
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
