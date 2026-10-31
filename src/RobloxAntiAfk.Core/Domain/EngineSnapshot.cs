namespace RobloxAntiAfk.Core.Domain;

public enum EngineState
{
    Running,
    Acting,
    PickingPoint,
    RecordingMacro,
    Stopped,
    WaitingForRoblox,
    PermissionRequired,
}

/// <summary>Неизменяемый снимок состояния движка для отображения.</summary>
public sealed record EngineSnapshot(
    EngineState State,
    TimeSpan TimeUntilNextAction,
    int ActionsThisRun,
    TimeSpan ActiveTimeThisRun,
    int WindowCount,
    AppSettings Settings,
    AfkStatistics Statistics,
    TimeSpan CurrentSession,
    SessionRecord? OngoingSession,
    IReadOnlyList<ActivityEntry> Activity)
{
    public bool IsHotkeyActive { get; init; }
    public string HotkeyLabel { get; init; } = "";
    public bool CanMuteRoblox { get; init; }
    public TimeSpan RecordingLeft { get; init; }
}
