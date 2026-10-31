using System.Text.Json.Serialization;

namespace RobloxAntiAfk.Core.Domain;

public enum MacroStepKind
{
    Key,
    MouseButton,
    MouseMove,
}

/// <summary>Один шаг записи. Положение мыши хранится в долях окна, поэтому окно можно двигать.</summary>
public sealed record MacroStep(
    double DelaySeconds,
    MacroStepKind Kind,
    bool IsDown = false,
    GameKey Key = default,
    MouseButton Button = default,
    WindowPoint? Point = null);

public sealed record Macro
{
    public IReadOnlyList<MacroStep> Steps { get; init; } = [];

    [JsonIgnore]
    public TimeSpan Duration => TimeSpan.FromSeconds(Steps.Sum(step => step.DelaySeconds));
}

/// <summary>Сырое событие ввода от системного перехвата во время записи.</summary>
public readonly record struct RawInputEvent(
    MacroStepKind Kind,
    bool IsDown = false,
    GameKey Key = default,
    MouseButton Button = default,
    ScreenPoint Point = default);
