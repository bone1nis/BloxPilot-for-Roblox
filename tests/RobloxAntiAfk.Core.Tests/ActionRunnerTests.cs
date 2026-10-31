using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;
using RobloxAntiAfk.Core.Tests.Fakes;

namespace RobloxAntiAfk.Core.Tests;

public class ActionRunnerTests
{
    private static async Task<(TestWorld World, ActionOutcome Outcome)> RunAsync(AppSettings settings, Action<TestWorld>? arrange = null)
    {
        var world = new TestWorld().WithRoblox(TestWorld.First);
        arrange?.Invoke(world);
        var runner = new ActionRunner(world.Platform, world.Clock);
        var outcome = await runner.RunAsync(world.Windows.Roblox, settings, cancellation: TestContext.Current.CancellationToken);
        return (world, outcome);
    }

    [Fact]
    public async Task Default_action_is_right_click_in_window_center_and_cursor_returns()
    {
        var (world, outcome) = await RunAsync(new AppSettings());

        Assert.Equal(1, outcome.WindowsDone);
        Assert.Equal(["move:500,500", "Right:down:500,500", "Right:up:500,500", "move:5,5"], world.Input.Events);
    }

    [Fact]
    public async Task Left_click_goes_to_saved_point()
    {
        var settings = new AppSettings { Action = AfkAction.LeftClick, ClickPoint = new WindowPoint(0.25, 0.5) };

        var (world, _) = await RunAsync(settings);

        Assert.Contains("Left:down:300,500", world.Input.Events);
        Assert.Contains("Left:up:300,500", world.Input.Events);
    }

    [Fact]
    public async Task Key_action_presses_selected_key()
    {
        var (world, _) = await RunAsync(new AppSettings { Action = AfkAction.Key, Key = GameKey.E });

        Assert.Equal(["E:down", "E:up"], world.Input.Events);
    }

    [Fact]
    public async Task Walk_presses_w_then_s()
    {
        var (world, _) = await RunAsync(new AppSettings { Action = AfkAction.Walk });

        Assert.Equal(["W:down", "W:up", "S:down", "S:up"], world.Input.Events);
    }

    [Fact]
    public async Task Zoom_presses_i_then_o()
    {
        var (world, _) = await RunAsync(new AppSettings { Action = AfkAction.Zoom });

        Assert.Equal(["I:down", "I:up", "O:down", "O:up"], world.Input.Events);
    }

    [Fact]
    public async Task Skips_when_user_is_playing_in_roblox()
    {
        var (world, outcome) = await RunAsync(new AppSettings(), w =>
        {
            w.Windows.Foreground = TestWorld.First;
            w.Idle.TimeSinceLastInput = TimeSpan.FromSeconds(10);
        });

        Assert.True(outcome.WasSkipped);
        Assert.Empty(world.Input.Events);
    }

    [Fact]
    public async Task Acts_while_playing_when_skipping_is_disabled()
    {
        var (_, outcome) = await RunAsync(new AppSettings { SkipWhilePlaying = false }, w =>
        {
            w.Windows.Foreground = TestWorld.First;
            w.Idle.TimeSinceLastInput = TimeSpan.FromSeconds(10);
        });

        Assert.Equal(1, outcome.WindowsDone);
    }

    [Fact]
    public async Task Returns_user_to_previous_window()
    {
        var (world, _) = await RunAsync(new AppSettings());

        Assert.Equal(["front:1", "front:999"], world.Windows.Log);
        Assert.Equal(TestWorld.OtherApp, world.Windows.Foreground);
    }

    [Fact]
    public async Task Waits_for_typing_pause_but_not_forever()
    {
        var (world, outcome) = await RunAsync(new AppSettings(), w => w.Idle.TimeSinceLastInput = TimeSpan.Zero);

        Assert.Equal(1, outcome.WindowsDone);
        Assert.True(world.Clock.TotalDelayed >= TimeSpan.FromSeconds(30));
        Assert.True(world.Clock.TotalDelayed < TimeSpan.FromSeconds(35));
    }

    [Fact]
    public async Task Acts_in_every_window_and_skips_the_one_being_played()
    {
        var world = new TestWorld().WithRoblox(TestWorld.First, TestWorld.Second);
        world.Windows.Foreground = TestWorld.First;
        world.Idle.TimeSinceLastInput = TimeSpan.FromSeconds(10);

        var outcome = await new ActionRunner(world.Platform, world.Clock).RunAsync(world.Windows.Roblox, new AppSettings(), cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.WindowsDone);
        Assert.Equal(2, outcome.WindowsTotal);
        Assert.Equal(["front:2", "front:1"], world.Windows.Log);
    }

    [Fact]
    public async Task Restores_minimized_window_and_minimizes_it_back()
    {
        var (world, outcome) = await RunAsync(new AppSettings(), w => w.Windows.Minimized.Add(TestWorld.First));

        Assert.Equal(1, outcome.WindowsDone);
        Assert.Equal(["restore:1", "front:1", "minimize:1", "front:999"], world.Windows.Log);
        Assert.Contains(TestWorld.First, world.Windows.Minimized);
    }

    [Fact]
    public async Task Does_not_send_input_when_switch_fails()
    {
        var (world, outcome) = await RunAsync(new AppSettings(), w => w.Windows.RefuseForeground.Add(TestWorld.First));

        Assert.Equal(WindowFailure.SwitchFailed, outcome.Failure);
        Assert.Equal(0, outcome.WindowsDone);
        Assert.Empty(world.Input.Events);
    }

    [Fact]
    public async Task Reports_unavailable_window_when_it_has_no_bounds()
    {
        var (world, outcome) = await RunAsync(new AppSettings(), w => w.Windows.Bounds.Clear());

        Assert.Equal(WindowFailure.WindowUnavailable, outcome.Failure);
        Assert.Empty(world.Input.Events);
    }
}
