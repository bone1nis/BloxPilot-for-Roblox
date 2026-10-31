using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;

namespace RobloxAntiAfk.Core.Services;

public enum WindowFailure
{
    None,
    SwitchFailed,
    WindowUnavailable,
}

public sealed record ActionOutcome(int WindowsDone, int WindowsTotal, WindowFailure Failure)
{
    public bool WasSkipped => WindowsDone == 0 && Failure == WindowFailure.None;
}

/// <summary>Выполняет действие в окнах Roblox и возвращает пользователя туда, где он был.</summary>
public sealed class ActionRunner(PlatformServices platform, IClock clock)
{
    private static readonly TimeSpan PlayingThreshold = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan TypingPause = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan BetweenWindows = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan InputStep = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan KeyHold = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan StepHold = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan ForegroundPoll = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MenuPause = TimeSpan.FromMilliseconds(400);
    private const int MaxTypingWaitSeconds = 30;
    private const int MaxForegroundPolls = 30;

    public async Task<ActionOutcome> RunAsync(
        IReadOnlyList<WindowHandle> windows,
        AppSettings settings,
        bool resetCharacter = false,
        CancellationToken cancellation = default)
    {
        var previous = platform.Windows.GetForegroundWindow();
        var done = 0;
        var failure = WindowFailure.None;
        var waitedForPause = false;

        for (var index = 0; index < windows.Count && !cancellation.IsCancellationRequested; index++)
        {
            if (index > 0)
            {
                await clock.Delay(BetweenWindows);
            }

            var window = windows[index];
            if (settings.SkipWhilePlaying && window == previous && platform.Idle.TimeSinceLastInput < PlayingThreshold)
            {
                continue;
            }

            if (platform.Windows.GetForegroundWindow() != window && !waitedForPause)
            {
                await WaitForTypingPauseAsync();
                waitedForPause = true;
            }

            var result = await ActOnWindowAsync(window, settings, resetCharacter, cancellation);
            if (result == WindowFailure.None)
            {
                done++;
            }
            else
            {
                failure = result;
            }
        }

        if (previous != WindowHandle.None && platform.Windows.GetForegroundWindow() != previous)
        {
            await BringToFrontAsync(previous);
        }

        return new ActionOutcome(done, windows.Count, failure);
    }

    public async Task<bool> BringToFrontAsync(WindowHandle window)
    {
        platform.Windows.RequestForeground(window);
        for (var poll = 0; poll < MaxForegroundPolls; poll++)
        {
            if (platform.Windows.GetForegroundWindow() == window)
            {
                return true;
            }

            await clock.Delay(ForegroundPoll);
        }

        return false;
    }

    /// <summary>Не перехватываем окно посреди набора текста, но и не ждём слишком долго.</summary>
    private async Task WaitForTypingPauseAsync()
    {
        for (var waited = 0; platform.Idle.TimeSinceLastInput < TypingPause && waited < MaxTypingWaitSeconds; waited++)
        {
            await clock.Delay(TimeSpan.FromSeconds(1));
        }
    }

    private async Task<WindowFailure> ActOnWindowAsync(WindowHandle window, AppSettings settings, bool resetCharacter, CancellationToken cancellation)
    {
        var wasMinimized = false;
        var wasInFront = platform.Windows.GetForegroundWindow() == window;
        if (!wasInFront)
        {
            wasMinimized = platform.Windows.IsMinimized(window);
            if (wasMinimized)
            {
                platform.Windows.Restore(window);
            }

            if (!await BringToFrontAsync(window))
            {
                if (wasMinimized)
                {
                    platform.Windows.Minimize(window);
                }

                return WindowFailure.SwitchFailed;
            }

            await clock.Delay(platform.WindowSettleDelay);
        }

        var bounds = platform.Windows.GetBounds(window);
        if (bounds is { } rect)
        {
            await SendActionAsync(settings, rect, cancellation);
            if (resetCharacter && !cancellation.IsCancellationRequested)
            {
                await ResetCharacterAsync();
            }
        }

        // Окно, в котором ты сейчас, не прячем: иначе игра свернётся прямо перед тобой.
        if (wasMinimized || (settings.HideBetweenActions && !wasInFront))
        {
            platform.Windows.Minimize(window);
        }

        return bounds is null ? WindowFailure.WindowUnavailable : WindowFailure.None;
    }

    private Task SendActionAsync(AppSettings settings, ScreenRect window, CancellationToken cancellation) => settings.Action switch
    {
        AfkAction.LeftClick => ClickAsync(MouseButton.Left, settings, window),
        AfkAction.RightClick => ClickAsync(MouseButton.Right, settings, window),
        AfkAction.Key => TapAsync(settings.Key, KeyHold),
        AfkAction.Walk => TapSequenceAsync(GameKey.W, GameKey.S, StepHold),
        AfkAction.Zoom => TapSequenceAsync(GameKey.I, GameKey.O, KeyHold),
        AfkAction.Macro when settings.Macro is { } macro => PlayMacroAsync(macro, window, cancellation),
        _ => Task.CompletedTask,
    };

    /// <summary>По умолчанию это правый клик: он сбрасывает таймер бездействия, но не нажимает кнопки в игре.</summary>
    private async Task ClickAsync(MouseButton button, AppSettings settings, ScreenRect window)
    {
        var target = settings.ClickPoint?.ToScreen(window) ?? window.Center;
        var cursor = platform.Input.GetCursor();

        platform.Input.MoveCursor(target);
        await clock.Delay(InputStep);
        platform.Input.SendMouseButton(button, isDown: true, target);
        await clock.Delay(InputStep);
        platform.Input.SendMouseButton(button, isDown: false, target);
        await clock.Delay(InputStep);
        platform.Input.MoveCursor(cursor);
    }

    /// <summary>Что бы ни случилось, все зажатые клавиши отпускаются, а курсор возвращается.</summary>
    private async Task PlayMacroAsync(Macro macro, ScreenRect window, CancellationToken cancellation)
    {
        var cursor = platform.Input.GetCursor();
        var heldKeys = new HashSet<GameKey>();
        var heldButtons = new Dictionary<MouseButton, ScreenPoint>();
        try
        {
            foreach (var step in macro.Steps)
            {
                await clock.Delay(TimeSpan.FromSeconds(step.DelaySeconds));
                if (cancellation.IsCancellationRequested)
                {
                    return;
                }

                var target = step.Point?.ToScreen(window) ?? window.Center;
                switch (step.Kind)
                {
                    case MacroStepKind.Key:
                        Track(heldKeys, step.Key, step.IsDown);
                        platform.Input.SendKey(step.Key, step.IsDown);
                        break;
                    case MacroStepKind.MouseMove:
                        platform.Input.MoveCursor(target);
                        break;
                    case MacroStepKind.MouseButton:
                        platform.Input.MoveCursor(target);
                        if (step.IsDown)
                        {
                            heldButtons[step.Button] = target;
                        }
                        else
                        {
                            heldButtons.Remove(step.Button);
                        }

                        platform.Input.SendMouseButton(step.Button, step.IsDown, target);
                        break;
                }
            }
        }
        finally
        {
            foreach (var key in heldKeys)
            {
                platform.Input.SendKey(key, isDown: false);
            }

            foreach (var (button, point) in heldButtons)
            {
                platform.Input.SendMouseButton(button, isDown: false, point);
            }

            platform.Input.MoveCursor(cursor);
        }
    }

    /// <summary>Обычный путь в меню Roblox: Esc открывает меню, R выбирает сброс, Enter подтверждает.</summary>
    private async Task ResetCharacterAsync()
    {
        await TapAsync(GameKey.Escape, KeyHold);
        await clock.Delay(MenuPause);
        await TapAsync(GameKey.R, KeyHold);
        await clock.Delay(MenuPause);
        await TapAsync(GameKey.Enter, KeyHold);
    }

    private static void Track(HashSet<GameKey> held, GameKey key, bool isDown)
    {
        if (isDown)
        {
            held.Add(key);
        }
        else
        {
            held.Remove(key);
        }
    }

    private async Task TapAsync(GameKey key, TimeSpan hold)
    {
        platform.Input.SendKey(key, isDown: true);
        await clock.Delay(hold);
        platform.Input.SendKey(key, isDown: false);
    }

    private async Task TapSequenceAsync(GameKey first, GameKey second, TimeSpan hold)
    {
        await TapAsync(first, hold);
        await clock.Delay(KeyHold);
        await TapAsync(second, hold);
    }
}
