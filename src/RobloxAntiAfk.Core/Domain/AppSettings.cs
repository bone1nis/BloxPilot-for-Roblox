using System.Text.Json.Serialization;

namespace RobloxAntiAfk.Core.Domain;

public sealed record AppSettings
{
    public const int MinIntervalMinutes = 1;

    /// <summary>Roblox обычно кикает примерно через 20 минут, поэтому интервал должен быть меньше.</summary>
    public const int MaxIntervalMinutes = 19;

    public int IntervalMinutes { get; init; } = 5;
    public AfkAction Action { get; init; } = AfkAction.RightClick;
    public GameKey Key { get; init; } = GameKey.Space;
    public WindowPoint? ClickPoint { get; init; }
    public bool AllWindows { get; init; } = true;
    public bool SkipWhilePlaying { get; init; } = true;
    public bool KeepAwake { get; init; } = true;

    [JsonIgnore]
    public TimeSpan Interval => TimeSpan.FromMinutes(IntervalMinutes);

    [JsonIgnore]
    public bool UsesMouse => Action is AfkAction.RightClick or AfkAction.LeftClick;

    public AppSettings Normalized() => this with
    {
        IntervalMinutes = Math.Clamp(IntervalMinutes, MinIntervalMinutes, MaxIntervalMinutes),
        ClickPoint = ClickPoint?.Clamped(),
    };
}
