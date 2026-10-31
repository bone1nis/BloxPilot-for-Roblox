namespace RobloxAntiAfk.Core.Domain;

/// <summary>Одна сессия: непрерывная работа от запуска до остановки или закрытия Roblox.</summary>
public sealed record SessionRecord(DateTimeOffset Start, double DurationSeconds, int Actions);

/// <summary>Статистика за всё время, сохраняется между запусками.</summary>
public sealed record AfkStatistics
{
    public double TotalSeconds { get; init; }
    public double LongestSessionSeconds { get; init; }
    public long TotalActions { get; init; }

    /// <summary>Завершённые сессии, самая новая первой.</summary>
    public IReadOnlyList<SessionRecord> Sessions { get; init; } = Array.Empty<SessionRecord>();
}
