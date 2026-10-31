using RobloxAntiAfk.Core.Domain;

namespace RobloxAntiAfk.Core.Services;

/// <summary>Собирает события ввода в макрос. Повторы при удержании клавиши и лишние движения мыши отбрасывает.</summary>
public sealed class MacroRecorder(DateTimeOffset start)
{
    public static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(60);
    public const int MaxSteps = 5000;

    private static readonly TimeSpan MinMoveGap = TimeSpan.FromMilliseconds(50);

    private readonly List<MacroStep> _steps = [];
    private readonly HashSet<GameKey> _heldKeys = [];
    private readonly HashSet<MouseButton> _heldButtons = [];
    private DateTimeOffset _last = start;
    private DateTimeOffset _lastMove = start - MinMoveGap;

    public bool IsFull => _steps.Count >= MaxSteps;

    public void Add(RawInputEvent input, ScreenRect? window, DateTimeOffset now)
    {
        if (IsFull)
        {
            return;
        }

        switch (input.Kind)
        {
            case MacroStepKind.Key when Track(_heldKeys, input.Key, input.IsDown):
                AddStep(now, new MacroStep(0, MacroStepKind.Key, input.IsDown, input.Key));
                break;
            case MacroStepKind.MouseButton when window is { } rect
                && (rect.Contains(input.Point) || !input.IsDown)
                && Track(_heldButtons, input.Button, input.IsDown):
                AddStep(now, new MacroStep(0, MacroStepKind.MouseButton, input.IsDown, Button: input.Button, Point: WindowPoint.FromScreen(input.Point, rect)));
                break;
            case MacroStepKind.MouseMove when window is { } rect && rect.Contains(input.Point) && now - _lastMove >= MinMoveGap:
                _lastMove = now;
                AddStep(now, new MacroStep(0, MacroStepKind.MouseMove, Point: WindowPoint.FromScreen(input.Point, rect)));
                break;
        }
    }

    /// <summary>Нажатия без отпускания убираем: это обычно само сочетание, которым остановили запись.</summary>
    public Macro Finish()
    {
        var unmatched = new List<MacroStep>();
        foreach (var key in _heldKeys)
        {
            unmatched.Add(_steps.Last(step => step.Kind == MacroStepKind.Key && step.IsDown && step.Key == key));
        }

        foreach (var button in _heldButtons)
        {
            unmatched.Add(_steps.Last(step => step.Kind == MacroStepKind.MouseButton && step.IsDown && step.Button == button));
        }

        return new Macro { Steps = _steps.Where(step => !unmatched.Any(extra => ReferenceEquals(extra, step))).ToArray() };
    }

    private static bool Track<T>(HashSet<T> held, T item, bool isDown) => isDown ? held.Add(item) : held.Remove(item);

    private void AddStep(DateTimeOffset now, MacroStep step)
    {
        var delay = _steps.Count == 0 ? 0 : (now - _last).TotalSeconds;
        _steps.Add(step with { DelaySeconds = delay });
        _last = now;
    }
}
