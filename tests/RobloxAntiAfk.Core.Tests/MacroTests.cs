using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;
using RobloxAntiAfk.Core.Tests.Fakes;

namespace RobloxAntiAfk.Core.Tests;

public class MacroTests
{
    private static readonly ScreenRect Window = new(100, 200, 800, 600);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static RawInputEvent Key(GameKey key, bool isDown) => new(MacroStepKind.Key, isDown, key);

    [Fact]
    public void Recorder_keeps_timing_and_drops_repeats_from_held_keys()
    {
        var recorder = new MacroRecorder(Start);

        recorder.Add(Key(GameKey.W, true), Window, Start.AddSeconds(5));
        recorder.Add(Key(GameKey.W, true), Window, Start.AddSeconds(5.5));
        recorder.Add(Key(GameKey.W, false), Window, Start.AddSeconds(6));

        var steps = recorder.Finish().Steps;
        Assert.Equal(2, steps.Count);
        Assert.Equal(0, steps[0].DelaySeconds);
        Assert.Equal(1, steps[1].DelaySeconds);
        Assert.Equal(1, new Macro { Steps = steps }.Duration.TotalSeconds);
    }

    [Fact]
    public void Recorder_ignores_key_release_that_started_before_recording()
    {
        var recorder = new MacroRecorder(Start);

        recorder.Add(Key(GameKey.W, false), Window, Start.AddSeconds(1));

        Assert.Empty(recorder.Finish().Steps);
    }

    [Fact]
    public void Recorder_drops_unreleased_keys_such_as_the_stop_hotkey()
    {
        var recorder = new MacroRecorder(Start);
        recorder.Add(Key(GameKey.W, true), Window, Start);
        recorder.Add(Key(GameKey.W, false), Window, Start.AddSeconds(1));
        recorder.Add(Key(GameKey.Control, true), Window, Start.AddSeconds(2));
        recorder.Add(Key(GameKey.Shift, true), Window, Start.AddSeconds(2));

        var steps = recorder.Finish().Steps;

        Assert.All(steps, step => Assert.Equal(GameKey.W, step.Key));
        Assert.Equal(2, steps.Count);
    }

    [Fact]
    public void Recorder_stores_clicks_relative_to_window_and_ignores_clicks_outside()
    {
        var recorder = new MacroRecorder(Start);
        var inside = new RawInputEvent(MacroStepKind.MouseButton, true, Button: MouseButton.Left, Point: new ScreenPoint(500, 500));
        var outside = new RawInputEvent(MacroStepKind.MouseButton, true, Button: MouseButton.Right, Point: new ScreenPoint(1, 1));
        var release = inside with { IsDown = false };

        recorder.Add(inside, Window, Start);
        recorder.Add(outside, Window, Start);
        recorder.Add(release, Window, Start.AddSeconds(1));

        var steps = recorder.Finish().Steps;
        Assert.Equal(2, steps.Count);
        Assert.Equal(new WindowPoint(0.5, 0.5), steps[0].Point);
    }

    [Fact]
    public void Recorder_limits_mouse_moves_to_twenty_per_second()
    {
        var recorder = new MacroRecorder(Start);
        var move = new RawInputEvent(MacroStepKind.MouseMove, Point: new ScreenPoint(200, 300));

        for (var millisecond = 0; millisecond < 100; millisecond += 10)
        {
            recorder.Add(move, Window, Start.AddMilliseconds(millisecond));
        }

        Assert.Equal(2, recorder.Finish().Steps.Count);
    }

    [Fact]
    public async Task Playback_places_mouse_by_window_and_returns_cursor()
    {
        var world = new TestWorld().WithRoblox(TestWorld.First);
        var settings = new AppSettings
        {
            Action = AfkAction.Macro,
            Macro = new Macro
            {
                Steps =
                [
                    new MacroStep(0, MacroStepKind.MouseButton, true, Button: MouseButton.Left, Point: new WindowPoint(0.5, 0.5)),
                    new MacroStep(0.1, MacroStepKind.MouseButton, false, Button: MouseButton.Left, Point: new WindowPoint(0.5, 0.5)),
                    new MacroStep(0.1, MacroStepKind.Key, true, GameKey.W),
                    new MacroStep(0.1, MacroStepKind.Key, false, GameKey.W),
                ],
            },
        };

        await new ActionRunner(world.Platform, world.Clock).RunAsync(world.Windows.Roblox, settings, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(
            ["move:500,500", "Left:down:500,500", "move:500,500", "Left:up:500,500", "W:down", "W:up", "move:5,5"],
            world.Input.Events);
    }

    [Fact]
    public async Task Stopping_mid_macro_releases_everything_it_holds()
    {
        var world = new TestWorld().WithRoblox(TestWorld.First);
        using var cancel = new CancellationTokenSource();
        var clock = new CancelOnLongDelayClock(world.Clock, cancel);
        var settings = new AppSettings
        {
            Action = AfkAction.Macro,
            Macro = new Macro
            {
                Steps =
                [
                    new MacroStep(0, MacroStepKind.Key, true, GameKey.W),
                    new MacroStep(0, MacroStepKind.MouseButton, true, Button: MouseButton.Right, Point: new WindowPoint(0.5, 0.5)),
                    new MacroStep(30, MacroStepKind.Key, true, GameKey.S),
                ],
            },
        };

        await new ActionRunner(world.Platform, clock).RunAsync(world.Windows.Roblox, settings, cancellation: cancel.Token);

        Assert.DoesNotContain("S:down", world.Input.Events);
        Assert.Contains("W:up", world.Input.Events);
        Assert.Contains("Right:up:500,500", world.Input.Events);
        Assert.Equal("move:5,5", world.Input.Events[^1]);
    }

    [Fact]
    public void Engine_records_only_while_roblox_is_in_front_and_saves_the_macro()
    {
        var world = new TestWorld().WithRoblox(TestWorld.First);
        var store = new MemoryStore<AppSettings>(new AppSettings());
        var engine = new AntiAfkEngine(world.Platform, world.Clock, store, new MemoryStore<AfkStatistics>(new AfkStatistics()));

        engine.BeginMacroRecording();
        Assert.Equal(EngineState.RecordingMacro, engine.Snapshot.State);
        Assert.True(world.Recorder.IsActive);

        world.Recorder.Emit(Key(GameKey.E, true));
        world.Clock.Advance(TimeSpan.FromSeconds(1));
        world.Recorder.Emit(Key(GameKey.E, false));

        world.Windows.Foreground = TestWorld.OtherApp;
        world.Recorder.Emit(Key(GameKey.Q, true));
        world.Recorder.Emit(Key(GameKey.Q, false));

        engine.StopMacroRecording();

        Assert.False(world.Recorder.IsActive);
        var steps = store.Value.Macro!.Steps;
        Assert.Equal(2, steps.Count);
        Assert.All(steps, step => Assert.Equal(GameKey.E, step.Key));
        Assert.Equal(ActivityCode.MacroSaved, engine.Snapshot.Activity[0].Code);
    }

    [Fact]
    public void Empty_recording_keeps_previous_macro()
    {
        var world = new TestWorld().WithRoblox(TestWorld.First);
        var existing = new Macro { Steps = [new MacroStep(0, MacroStepKind.Key, true, GameKey.W)] };
        var store = new MemoryStore<AppSettings>(new AppSettings { Macro = existing });
        var engine = new AntiAfkEngine(world.Platform, world.Clock, store, new MemoryStore<AfkStatistics>(new AfkStatistics()));

        engine.BeginMacroRecording();
        engine.StopMacroRecording();

        Assert.Same(existing, store.Value.Macro);
        Assert.Equal(ActivityCode.MacroEmpty, engine.Snapshot.Activity[0].Code);
    }

    [Fact]
    public void Recording_stops_by_itself_after_the_time_limit()
    {
        var world = new TestWorld().WithRoblox(TestWorld.First);
        var engine = new AntiAfkEngine(world.Platform, world.Clock, new MemoryStore<AppSettings>(new AppSettings()), new MemoryStore<AfkStatistics>(new AfkStatistics()));

        engine.BeginMacroRecording();
        world.Clock.Advance(MacroRecorder.MaxDuration);
        engine.Tick();

        Assert.False(world.Recorder.IsActive);
        Assert.NotEqual(EngineState.RecordingMacro, engine.Snapshot.State);
    }

    [Fact]
    public void Macro_action_without_recording_presses_nothing_and_explains_why()
    {
        var world = new TestWorld().WithRoblox(TestWorld.First);
        var settings = new MemoryStore<AppSettings>(new AppSettings { Action = AfkAction.Macro });
        var engine = new AntiAfkEngine(world.Platform, world.Clock, settings, new MemoryStore<AfkStatistics>(new AfkStatistics()));
        engine.ToggleRunning();

        engine.RunNow();

        Assert.Empty(world.Input.Events);
        Assert.Equal(ActivityCode.MacroMissing, engine.Snapshot.Activity[0].Code);
    }

    private sealed class CancelOnLongDelayClock(IClock inner, CancellationTokenSource cancel) : IClock
    {
        public DateTimeOffset Now => inner.Now;

        public Task Delay(TimeSpan duration)
        {
            if (duration >= TimeSpan.FromSeconds(30))
            {
                cancel.Cancel();
            }

            return inner.Delay(duration);
        }

        public Task Yield() => inner.Yield();
    }
}
