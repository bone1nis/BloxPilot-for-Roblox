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
    public void Stopping_records_session_with_start_duration_and_actions()
    {
        var store = new MemoryStore<AfkStatistics>(new AfkStatistics());
        var tracker = new StatisticsTracker(store, Start);

        tracker.Update(isActive: true, Start.AddSeconds(30));
        tracker.RecordActions(2);
        Assert.Equal(new SessionRecord(Start, 30, 2), tracker.OngoingSession);
        Assert.Empty(tracker.Current.Sessions);

        tracker.Update(isActive: false, Start.AddSeconds(31));

        Assert.Null(tracker.OngoingSession);
        Assert.Equal([new SessionRecord(Start, 30, 2)], store.Value.Sessions);
    }

    [Fact]
    public void Newest_session_comes_first_and_actions_do_not_carry_over()
    {
        var tracker = new StatisticsTracker(new MemoryStore<AfkStatistics>(new AfkStatistics()), Start);

        tracker.Update(isActive: true, Start.AddSeconds(20));
        tracker.RecordActions(5);
        tracker.Update(isActive: false, Start.AddSeconds(25));
        tracker.Update(isActive: true, Start.AddSeconds(45));
        tracker.Update(isActive: false, Start.AddSeconds(50));

        Assert.Equal(
            [new SessionRecord(Start.AddSeconds(25), 20, 0), new SessionRecord(Start, 20, 5)],
            tracker.Current.Sessions);
    }

    [Fact]
    public void Very_short_session_is_not_recorded()
    {
        var tracker = new StatisticsTracker(new MemoryStore<AfkStatistics>(new AfkStatistics()), Start);

        tracker.Update(isActive: true, Start.AddSeconds(5));
        tracker.Update(isActive: false, Start.AddSeconds(6));

        Assert.Empty(tracker.Current.Sessions);
    }

    [Fact]
    public void Keeps_only_recent_sessions()
    {
        var existing = Enumerable.Range(0, StatisticsTracker.MaxRecordedSessions)
            .Select(index => new SessionRecord(Start.AddDays(-index - 1), 60, 1))
            .ToArray();
        var tracker = new StatisticsTracker(new MemoryStore<AfkStatistics>(new AfkStatistics { Sessions = existing }), Start);

        tracker.Update(isActive: true, Start.AddSeconds(20));
        tracker.Update(isActive: false, Start.AddSeconds(21));

        Assert.Equal(StatisticsTracker.MaxRecordedSessions, tracker.Current.Sessions.Count);
        Assert.Equal(Start, tracker.Current.Sessions[0].Start);
    }

    [Fact]
    public void Finish_records_running_session_and_saves()
    {
        var store = new MemoryStore<AfkStatistics>(new AfkStatistics());
        var tracker = new StatisticsTracker(store, Start);
        tracker.Update(isActive: true, Start.AddSeconds(20));

        tracker.Finish();

        Assert.Single(store.Value.Sessions);
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
