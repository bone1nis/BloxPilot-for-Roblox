using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Storage;

namespace RobloxAntiAfk.Core.Services;

/// <summary>Считает время работы, действия и сессии. Сессия прерывается остановкой приложения или закрытием Roblox.</summary>
public sealed class StatisticsTracker(IDataStore<AfkStatistics> store, DateTimeOffset now)
{
    /// <summary>Большой разрыв между обновлениями означает, что компьютер спал: это время не считаем.</summary>
    private static readonly TimeSpan MaxCountedGap = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan SaveInterval = TimeSpan.FromMinutes(1);

    /// <summary>Случайное включение и выключение не засоряет список.</summary>
    internal static readonly TimeSpan MinRecordedSession = TimeSpan.FromSeconds(10);

    internal const int MaxRecordedSessions = 50;

    private DateTimeOffset _lastUpdate = now;
    private DateTimeOffset _lastSave = now;
    private DateTimeOffset? _sessionStart;
    private int _sessionActions;

    public AfkStatistics Current { get; private set; } = store.Load();

    public TimeSpan CurrentSession { get; private set; }

    /// <summary>Сколько приложение работало с момента открытия окна. Не сохраняется между запусками.</summary>
    public TimeSpan ActiveTimeThisRun { get; private set; }

    public SessionRecord? OngoingSession =>
        _sessionStart is { } start ? new SessionRecord(start, CurrentSession.TotalSeconds, _sessionActions) : null;

    public void Update(bool isActive, DateTimeOffset time)
    {
        var elapsed = time - _lastUpdate;
        elapsed = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed > MaxCountedGap ? MaxCountedGap : elapsed;
        _lastUpdate = time;

        if (isActive)
        {
            _sessionStart ??= time - elapsed;
            CurrentSession += elapsed;
            ActiveTimeThisRun += elapsed;
            Current = Current with
            {
                TotalSeconds = Current.TotalSeconds + elapsed.TotalSeconds,
                LongestSessionSeconds = Math.Max(Current.LongestSessionSeconds, CurrentSession.TotalSeconds),
            };
        }
        else
        {
            EndSession();
        }

        if (time - _lastSave >= SaveInterval)
        {
            _lastSave = time;
            store.Save(Current);
        }
    }

    public void RecordActions(int count)
    {
        _sessionActions += count;
        Current = Current with { TotalActions = Current.TotalActions + count };
    }

    public void Reset()
    {
        Current = new AfkStatistics();
        CurrentSession = TimeSpan.Zero;
        _sessionStart = null;
        _sessionActions = 0;
        store.Save(Current);
    }

    /// <summary>Закрывает текущую сессию и сохраняет всё перед выходом из приложения.</summary>
    public void Finish()
    {
        EndSession();
        store.Save(Current);
    }

    private void EndSession()
    {
        if (_sessionStart is { } start && CurrentSession >= MinRecordedSession)
        {
            var record = new SessionRecord(start, CurrentSession.TotalSeconds, _sessionActions);
            Current = Current with { Sessions = [record, .. Current.Sessions.Take(MaxRecordedSessions - 1)] };
            store.Save(Current);
        }

        _sessionStart = null;
        _sessionActions = 0;
        CurrentSession = TimeSpan.Zero;
    }
}
