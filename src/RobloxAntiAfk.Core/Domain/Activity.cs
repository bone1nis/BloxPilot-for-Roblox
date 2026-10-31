namespace RobloxAntiAfk.Core.Domain;

public enum ActivitySeverity
{
    Success,
    Skipped,
    Error,
}

/// <summary>Что произошло. Текст для пользователя собирает слой интерфейса.</summary>
public enum ActivityCode
{
    ActionDone,
    SkippedWhilePlaying,
    SwitchFailed,
    WindowUnavailable,
    PointSaved,
    PointPickCancelled,
    PointPickFailed,
    RobloxNotRunning,
    PermissionMissing,
    UnexpectedError,
    CharacterReset,
    MacroSaved,
    MacroEmpty,
    MacroMissing,
    MacroRecordFailed,
    RobloxClosed,
    Disconnected,
    Reconnecting,
    Reconnected,
    ReconnectGaveUp,
}

public sealed record ActivityEntry(
    DateTimeOffset Time,
    ActivitySeverity Severity,
    ActivityCode Code,
    AfkAction Action = default,
    GameKey Key = default,
    int WindowsDone = 0,
    int WindowsTotal = 0);
