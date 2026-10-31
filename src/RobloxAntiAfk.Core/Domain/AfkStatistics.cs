namespace RobloxAntiAfk.Core.Domain;

/// <summary>Статистика за всё время, сохраняется между запусками.</summary>
public sealed record AfkStatistics
{
    public double TotalSeconds { get; init; }
    public double LongestSessionSeconds { get; init; }
    public long TotalActions { get; init; }
}
