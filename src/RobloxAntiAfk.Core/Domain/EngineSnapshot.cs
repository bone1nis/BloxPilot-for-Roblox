namespace RobloxAntiAfk.Core.Domain;

public enum EngineState
{
    Running,
    Acting,
    PickingPoint,
    Paused,
    WaitingForRoblox,
    PermissionRequired,
}

/// <summary>Неизменяемый снимок состояния движка для отображения.</summary>
public sealed record EngineSnapshot(
    EngineState State,
    TimeSpan TimeUntilNextAction,
    int ActionsThisRun,
    TimeSpan Uptime,
    int WindowCount,
    AppSettings Settings,
    AfkStatistics Statistics,
    TimeSpan CurrentSession,
    IReadOnlyList<ActivityEntry> Activity);
