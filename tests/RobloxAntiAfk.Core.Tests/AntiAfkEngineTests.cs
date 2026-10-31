using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;
using RobloxAntiAfk.Core.Tests.Fakes;

namespace RobloxAntiAfk.Core.Tests;

public class AntiAfkEngineTests
{
    private readonly TestWorld _world = new TestWorld().WithRoblox(TestWorld.First);
    private readonly MemoryStore<AppSettings> _settings = new(new AppSettings());
    private readonly MemoryStore<AfkStatistics> _statistics = new(new AfkStatistics());

    private AntiAfkEngine CreateStoppedEngine() => new(_world.Platform, _world.Clock, _settings, _statistics);

    private AntiAfkEngine CreateEngine()
    {
        var engine = CreateStoppedEngine();
        engine.ToggleRunning();
        return engine;
    }

    [Fact]
    public void Opens_stopped_without_actions_prompts_or_sleep_block()
    {
        _world.Permission.IsGranted = false;
        var engine = CreateStoppedEngine();
        engine.Start();
        _world.Clock.Advance(TimeSpan.FromHours(1));

        engine.Tick();

        Assert.Equal(EngineState.Stopped, engine.Snapshot.State);
        Assert.Empty(_world.Input.Events);
        Assert.Equal(0, _world.Permission.Prompts);
        Assert.Equal(SleepMode.Allow, _world.Sleep.Mode);
    }

    [Fact]
    public void Working_time_counts_only_while_started()
    {
        var engine = CreateStoppedEngine();
        _world.Clock.Advance(TimeSpan.FromMinutes(1));
        engine.Tick();
        Assert.Equal(TimeSpan.Zero, engine.Snapshot.ActiveTimeThisRun);

        engine.ToggleRunning();
        _world.Clock.Advance(TimeSpan.FromSeconds(30));
        engine.Tick();
        Assert.Equal(TimeSpan.FromSeconds(30), engine.Snapshot.ActiveTimeThisRun);

        engine.ToggleRunning();
        _world.Clock.Advance(TimeSpan.FromMinutes(1));
        engine.Tick();

        var snapshot = engine.Snapshot;
        Assert.Equal(TimeSpan.FromSeconds(30), snapshot.ActiveTimeThisRun);
        Assert.Equal(TimeSpan.Zero, snapshot.CurrentSession);
        Assert.Equal(30, snapshot.Statistics.TotalSeconds);
    }

    [Fact]
    public void Picking_point_while_stopped_does_not_count_as_work()
    {
        var engine = CreateStoppedEngine();
        engine.BeginPointPick();

        _world.Clock.Advance(TimeSpan.FromSeconds(10));
        engine.Tick();

        Assert.Equal(EngineState.PickingPoint, engine.Snapshot.State);
        Assert.Equal(TimeSpan.Zero, engine.Snapshot.ActiveTimeThisRun);
    }

    [Fact]
    public void Starting_begins_full_interval_and_asks_for_missing_permission()
    {
        _world.Permission.IsGranted = false;
        var engine = CreateStoppedEngine();
        _world.Clock.Advance(TimeSpan.FromHours(1));

        engine.ToggleRunning();

        Assert.Equal(1, _world.Permission.Prompts);
        Assert.Equal(EngineState.PermissionRequired, engine.Snapshot.State);
        Assert.Equal(TimeSpan.FromMinutes(5), engine.Snapshot.TimeUntilNextAction);
    }

    [Fact]
    public void Run_now_does_nothing_while_stopped()
    {
        var engine = CreateStoppedEngine();

        engine.RunNow();

        Assert.Empty(_world.Input.Events);
    }

    [Fact]
    public void Does_nothing_before_interval_passes()
    {
        var engine = CreateEngine();
        _world.Clock.Advance(TimeSpan.FromMinutes(4));

        engine.Tick();

        Assert.Empty(_world.Input.Events);
        Assert.Equal(EngineState.Running, engine.Snapshot.State);
        Assert.Equal(TimeSpan.FromMinutes(1), engine.Snapshot.TimeUntilNextAction);
    }

    [Fact]
    public void Acts_when_interval_passes_and_schedules_next()
    {
        var engine = CreateEngine();
        _world.Clock.Advance(TimeSpan.FromMinutes(5));

        engine.Tick();

        var snapshot = engine.Snapshot;
        Assert.Equal(1, snapshot.ActionsThisRun);
        Assert.Equal(1, snapshot.Statistics.TotalActions);
        Assert.Equal(ActivityCode.ActionDone, snapshot.Activity[0].Code);
        Assert.Equal(TimeSpan.FromMinutes(5), snapshot.TimeUntilNextAction);
    }

    [Fact]
    public void Waits_when_roblox_is_not_running_and_does_not_fire_immediately_after_it_starts()
    {
        _world.Windows.Roblox.Clear();
        var engine = CreateEngine();
        _world.Clock.Advance(TimeSpan.FromMinutes(10));

        engine.Tick();
        Assert.Equal(EngineState.WaitingForRoblox, engine.Snapshot.State);

        _world.WithRoblox(TestWorld.First);
        engine.Tick();

        Assert.Equal(EngineState.Running, engine.Snapshot.State);
        Assert.Empty(_world.Input.Events);
    }

    [Fact]
    public void Requires_permission_before_acting()
    {
        _world.Permission.IsGranted = false;
        var engine = CreateEngine();
        _world.Clock.Advance(TimeSpan.FromMinutes(10));

        engine.Tick();

        Assert.Equal(EngineState.PermissionRequired, engine.Snapshot.State);
        Assert.Empty(_world.Input.Events);
    }

    [Fact]
    public void Stopping_ends_actions_and_allows_sleep()
    {
        var engine = CreateEngine();
        engine.Start();
        Assert.Equal(SleepMode.KeepAwake, _world.Sleep.Mode);

        engine.ToggleRunning();
        _world.Clock.Advance(TimeSpan.FromMinutes(10));
        engine.Tick();

        Assert.Equal(EngineState.Stopped, engine.Snapshot.State);
        Assert.Equal(SleepMode.Allow, _world.Sleep.Mode);
        Assert.Empty(_world.Input.Events);
    }

    [Fact]
    public void Run_now_acts_immediately()
    {
        var engine = CreateEngine();

        engine.RunNow();

        Assert.Equal(1, engine.Snapshot.ActionsThisRun);
    }

    [Fact]
    public void Changing_interval_restarts_countdown_and_persists()
    {
        var engine = CreateEngine();

        engine.UpdateSettings(s => s with { IntervalMinutes = 10 });

        Assert.Equal(TimeSpan.FromMinutes(10), engine.Snapshot.TimeUntilNextAction);
        Assert.Equal(10, _settings.Value.IntervalMinutes);
    }

    [Fact]
    public void Interval_is_clamped_below_kick_time()
    {
        var engine = CreateEngine();

        engine.UpdateSettings(s => s with { IntervalMinutes = 60 });

        Assert.Equal(AppSettings.MaxIntervalMinutes, engine.Snapshot.Settings.IntervalMinutes);
    }

    [Fact]
    public void Keep_awake_setting_changes_sleep_mode()
    {
        var engine = CreateEngine();

        engine.UpdateSettings(s => s with { KeepAwake = false });

        Assert.Equal(SleepMode.AllowButStayResponsive, _world.Sleep.Mode);
    }

    [Fact]
    public void All_windows_off_uses_only_first_window()
    {
        _world.WithRoblox(TestWorld.Second);
        var engine = CreateEngine();
        engine.UpdateSettings(s => s with { AllWindows = false });

        engine.RunNow();

        Assert.Equal(1, engine.Snapshot.WindowCount);
        Assert.Equal(1, engine.Snapshot.ActionsThisRun);
    }

    [Fact]
    public void Clicking_inside_roblox_saves_point_and_swallows_click()
    {
        var engine = CreateEngine();
        engine.BeginPointPick();
        Assert.Equal(EngineState.PickingPoint, engine.Snapshot.State);

        var swallowed = _world.Capture.Click(new ScreenPoint(300, 500));

        Assert.True(swallowed);
        Assert.Equal(new WindowPoint(0.25, 0.5), _settings.Value.ClickPoint);
        Assert.False(_world.Capture.IsActive);
        Assert.Equal(TestWorld.OtherApp, _world.Windows.Foreground);
        Assert.Equal(ActivityCode.PointSaved, engine.Snapshot.Activity[0].Code);
    }

    [Fact]
    public void Clicks_outside_roblox_pass_through_and_keep_waiting()
    {
        var engine = CreateEngine();
        engine.BeginPointPick();

        var swallowed = _world.Capture.Click(new ScreenPoint(10, 10));

        Assert.False(swallowed);
        Assert.True(_world.Capture.IsActive);
        Assert.Null(_settings.Value.ClickPoint);
    }

    [Fact]
    public void Point_pick_times_out()
    {
        var engine = CreateEngine();
        engine.BeginPointPick();

        _world.Clock.Advance(TimeSpan.FromSeconds(31));
        engine.Tick();

        Assert.False(_world.Capture.IsActive);
        Assert.Equal(ActivityCode.PointPickCancelled, engine.Snapshot.Activity[0].Code);
        Assert.NotEqual(EngineState.PickingPoint, engine.Snapshot.State);
    }

    [Fact]
    public void No_actions_are_sent_while_picking_point()
    {
        var engine = CreateEngine();
        _world.Clock.Advance(TimeSpan.FromMinutes(5));
        engine.BeginPointPick();

        engine.Tick();

        Assert.Empty(_world.Input.Events);
    }

    [Fact]
    public void Point_pick_reports_when_roblox_is_missing()
    {
        _world.Windows.Roblox.Clear();
        var engine = CreateEngine();

        engine.BeginPointPick();

        Assert.False(_world.Capture.IsActive);
        Assert.Equal(ActivityCode.RobloxNotRunning, engine.Snapshot.Activity[0].Code);
    }

    [Fact]
    public void Reset_settings_restores_defaults()
    {
        var engine = CreateEngine();
        engine.UpdateSettings(s => s with { IntervalMinutes = 2, Action = AfkAction.Zoom, ClickPoint = new WindowPoint(1, 1) });

        engine.ResetSettings();

        Assert.Equal(new AppSettings(), engine.Snapshot.Settings);
    }

    [Fact]
    public void Shutdown_saves_statistics_and_releases_system_hooks()
    {
        var engine = CreateEngine();
        engine.Start();
        engine.RunNow();

        engine.Shutdown();

        Assert.Equal(1, _statistics.Value.TotalActions);
        Assert.Equal(SleepMode.Allow, _world.Sleep.Mode);
    }
}
