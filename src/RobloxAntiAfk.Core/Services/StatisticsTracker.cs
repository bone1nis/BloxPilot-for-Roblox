using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Storage;

namespace RobloxAntiAfk.Core.Services;

/// <summary>Считает время работы и действия. Сессия прерывается паузой или закрытием Roblox.</summary>
public sealed class StatisticsTracker(IDataStore<AfkStatistics> store, DateTimeOffset now)
{
    /// <summary>Большой разрыв между обновлениями означает, что компьютер спал: это время не считаем.</summary>
    private static readonly TimeSpan MaxCountedGap = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan SaveInterval = TimeSpan.FromMinutes(1);

    private DateTimeOffset _lastUpdate = now;
    private DateTimeOffset _lastSave = now;

    public AfkStatistics Current { get; private set; } = store.Load();

    public TimeSpan CurrentSession { get; private set; }

    public void Update(bool isActive, DateTimeOffset time)
    {
        var elapsed = time - _lastUpdate;
        elapsed = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed > MaxCountedGap ? MaxCountedGap : elapsed;
        _lastUpdate = time;

        if (isActive)
        {
            CurrentSession += elapsed;
            Current = Current with
            {
                TotalSeconds = Current.TotalSeconds + elapsed.TotalSeconds,
                LongestSessionSeconds = Math.Max(Current.LongestSessionSeconds, CurrentSession.TotalSeconds),
            };
        }
        else
        {
            CurrentSession = TimeSpan.Zero;
        }

        if (time - _lastSave >= SaveInterval)
        {
            _lastSave = time;
            store.Save(Current);
        }
    }

    public void RecordActions(int count) => Current = Current with { TotalActions = Current.TotalActions + count };

    public void Reset()
    {
        Current = new AfkStatistics();
        CurrentSession = TimeSpan.Zero;
        store.Save(Current);
    }

    public void Save() => store.Save(Current);
}
