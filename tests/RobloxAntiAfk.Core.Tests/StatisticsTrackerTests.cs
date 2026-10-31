using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;
using RobloxAntiAfk.Core.Tests.Fakes;

namespace RobloxAntiAfk.Core.Tests;

public class StatisticsTrackerTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Counts_active_time_and_longest_session()
    {
        var tracker = new StatisticsTracker(new MemoryStore<AfkStatistics>(new AfkStatistics()), Start);

        tracker.Update(isActive: true, Start.AddSeconds(30));
        tracker.Update(isActive: true, Start.AddSeconds(60));

        Assert.Equal(60, tracker.Current.TotalSeconds);
        Assert.Equal(60, tracker.Current.LongestSessionSeconds);
        Assert.Equal(TimeSpan.FromSeconds(60), tracker.CurrentSession);
    }

    [Fact]
    public void Inactive_time_ends_session_but_keeps_longest()
    {
        var tracker = new StatisticsTracker(new MemoryStore<AfkStatistics>(new AfkStatistics()), Start);

        tracker.Update(isActive: true, Start.AddSeconds(60));
        tracker.Update(isActive: false, Start.AddSeconds(90));
        tracker.Update(isActive: true, Start.AddSeconds(100));

        Assert.Equal(70, tracker.Current.TotalSeconds);
        Assert.Equal(60, tracker.Current.LongestSessionSeconds);
        Assert.Equal(TimeSpan.FromSeconds(10), tracker.CurrentSession);
    }

    [Fact]
    public void Long_gap_from_computer_sleep_is_not_counted_in_full()
    {
        var tracker = new StatisticsTracker(new MemoryStore<AfkStatistics>(new AfkStatistics()), Start);

        tracker.Update(isActive: true, Start.AddHours(8));

        Assert.Equal(120, tracker.Current.TotalSeconds);
    }

    [Fact]
    public void Saves_periodically_and_reset_clears_everything()
    {
        var store = new MemoryStore<AfkStatistics>(new AfkStatistics());
        var tracker = new StatisticsTracker(store, Start);

        tracker.Update(isActive: true, Start.AddSeconds(30));
        Assert.Equal(0, store.Saves);
        tracker.Update(isActive: true, Start.AddSeconds(60));
        Assert.Equal(1, store.Saves);

        tracker.RecordActions(3);
        tracker.Reset();

        Assert.Equal(new AfkStatistics(), store.Value);
        Assert.Equal(TimeSpan.Zero, tracker.CurrentSession);
    }
}
