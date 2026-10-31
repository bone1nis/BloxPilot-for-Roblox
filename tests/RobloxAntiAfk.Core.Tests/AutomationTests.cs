using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;
using RobloxAntiAfk.Core.Tests.Fakes;

namespace RobloxAntiAfk.Core.Tests;

public class AutomationTests
{
    private const string LaunchUri = "roblox://experiences/start?placeId=123";

    private readonly TestWorld _world = new TestWorld().WithRoblox(TestWorld.First);

    private AntiAfkEngine CreateEngine(AppSettings? settings = null) => new(
        _world.Platform,
        _world.Clock,
        new MemoryStore<AppSettings>(settings ?? new AppSettings()),
        new MemoryStore<AfkStatistics>(new AfkStatistics()));

    private AntiAfkEngine CreateRunning(AppSettings? settings = null)
    {
        var engine = CreateEngine(settings);
        engine.ToggleRunning();
        return engine;
    }

    private void Pass(AntiAfkEngine engine, TimeSpan time)
    {
        _world.Clock.Advance(time);
        engine.Tick();
    }

    private static AppSettings Reconnecting => new() { ReconnectEnabled = true, GameLink = "123" };

    [Fact]
    public async Task Character_reset_sends_escape_r_enter_after_the_action()
    {
        var runner = new ActionRunner(_world.Platform, _world.Clock);

        await runner.RunAsync(_world.Windows.Roblox, new AppSettings { Action = AfkAction.Zoom }, resetCharacter: true, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(
            ["I:down", "I:up", "O:down", "O:up", "Escape:down", "Escape:up", "R:down", "R:up", "Enter:down", "Enter:up"],
            _world.Input.Events);
    }

    [Fact]
    public void Engine_resets_character_only_when_due()
    {
        var engine = CreateRunning(new AppSettings { ResetMinutes = 15, IntervalMinutes = 5 });

        Pass(engine, TimeSpan.FromMinutes(5));
        Assert.DoesNotContain("Escape:down", _world.Input.Events);

        Pass(engine, TimeSpan.FromMinutes(11));
        Assert.Contains("Escape:down", _world.Input.Events);
        Assert.Contains(engine.Snapshot.Activity, entry => entry.Code == ActivityCode.CharacterReset);
    }

    [Fact]
    public void Character_reset_is_off_by_default()
    {
        var engine = CreateRunning();

        Pass(engine, TimeSpan.FromHours(1));

        Assert.DoesNotContain("Escape:down", _world.Input.Events);
    }

    [Fact]
    public async Task Hide_setting_minimizes_background_window_after_the_action()
    {
        var runner = new ActionRunner(_world.Platform, _world.Clock);

        await runner.RunAsync(_world.Windows.Roblox, new AppSettings { HideBetweenActions = true }, cancellation: TestContext.Current.CancellationToken);

        Assert.Contains("minimize:1", _world.Windows.Log);
    }

    [Fact]
    public async Task Hide_setting_never_hides_the_window_you_are_in()
    {
        _world.Windows.Foreground = TestWorld.First;
        var runner = new ActionRunner(_world.Platform, _world.Clock);

        await runner.RunAsync(_world.Windows.Roblox, new AppSettings { HideBetweenActions = true, SkipWhilePlaying = false }, cancellation: TestContext.Current.CancellationToken);

        Assert.DoesNotContain("minimize:1", _world.Windows.Log);
    }

    [Fact]
    public void Hotkey_toggles_running_and_stops_recording()
    {
        var engine = CreateEngine();
        engine.Start();
        Assert.True(engine.Snapshot.IsHotkeyActive);

        _world.Hotkey.Press();
        engine.Tick();
        Assert.Equal(EngineState.Running, engine.Snapshot.State);

        engine.BeginMacroRecording();
        _world.Hotkey.Press();
        Assert.False(_world.Recorder.IsActive);
        Assert.Equal(EngineState.Running, engine.Snapshot.State);

        engine.Shutdown();
        _world.Hotkey.Press();
        Assert.Equal(EngineState.Running, engine.Snapshot.State);
    }

    [Fact]
    public void Busy_hotkey_is_reported_but_does_not_break_start()
    {
        _world.Hotkey.CanStart = false;
        var engine = CreateEngine();

        engine.Start();

        Assert.False(engine.Snapshot.IsHotkeyActive);
    }

    [Fact]
    public void Run_on_open_starts_immediately_only_when_enabled()
    {
        var engine = CreateEngine(new AppSettings { RunOnOpen = true });

        engine.Start();

        Assert.Equal(EngineState.Running, engine.Snapshot.State);
    }

    [Fact]
    public void Autostart_follows_the_setting_and_reset_turns_it_off()
    {
        var engine = CreateEngine();

        engine.UpdateSettings(settings => settings with { StartWithSystem = true });
        engine.UpdateSettings(settings => settings with { IntervalMinutes = 7 });
        engine.ResetSettings();

        Assert.Equal([true, false], _world.AutoStart.Applied);
    }

    [Fact]
    public void Reset_keeps_macro_link_and_telegram()
    {
        var macro = new Macro { Steps = [new MacroStep(0, MacroStepKind.Key, true, GameKey.W)] };
        var engine = CreateEngine(new AppSettings
        {
            IntervalMinutes = 9,
            Macro = macro,
            GameLink = "123",
            TelegramToken = "1:abc",
            TelegramChatId = "5",
            ReconnectEnabled = true,
        });

        engine.ResetSettings();

        var settings = engine.Snapshot.Settings;
        Assert.Equal(5, settings.IntervalMinutes);
        Assert.False(settings.ReconnectEnabled);
        Assert.Same(macro, settings.Macro);
        Assert.Equal("123", settings.GameLink);
        Assert.Equal("5", settings.TelegramChatId);
    }

    [Fact]
    public void Sound_is_muted_while_running_and_restored_on_stop()
    {
        var engine = CreateRunning(new AppSettings { MuteRoblox = true });
        Assert.Equal([true], _world.Audio.Calls);

        engine.ToggleRunning();

        Assert.Equal([true, false], _world.Audio.Calls);
    }

    [Fact]
    public void Sound_is_untouched_when_the_setting_is_off()
    {
        var engine = CreateRunning();

        engine.ToggleRunning();

        Assert.Empty(_world.Audio.Calls);
    }

    [Fact]
    public void Shutdown_restores_sound()
    {
        var engine = CreateRunning(new AppSettings { MuteRoblox = true });

        engine.Shutdown();

        Assert.False(_world.Audio.Calls[^1]);
    }

    [Fact]
    public void Mute_is_repeated_because_a_new_game_gets_sound_later()
    {
        var engine = CreateRunning(new AppSettings { MuteRoblox = true });

        Pass(engine, TimeSpan.FromSeconds(31));

        Assert.Equal([true, true], _world.Audio.Calls);
    }

    [Fact]
    public void Starting_ignores_disconnects_that_happened_before()
    {
        CreateRunning(Reconnecting);

        Assert.Equal(1, _world.Disconnects.Arms);
    }

    [Fact]
    public void Disconnect_opens_the_game_and_later_reports_success()
    {
        var engine = CreateRunning(Reconnecting);

        _world.Disconnects.IsDisconnected = true;
        Pass(engine, TimeSpan.FromSeconds(1));

        Assert.Equal([LaunchUri], _world.Launcher.Opened);
        Assert.Contains(engine.Snapshot.Activity, entry => entry.Code == ActivityCode.Disconnected);
        Assert.Contains(engine.Snapshot.Activity, entry => entry.Code == ActivityCode.Reconnecting);

        Pass(engine, TimeSpan.FromSeconds(91));

        Assert.Contains(engine.Snapshot.Activity, entry => entry.Code == ActivityCode.Reconnected);
    }

    [Fact]
    public void Gives_up_after_five_attempts_in_a_row()
    {
        var engine = CreateRunning(Reconnecting);

        for (var attempt = 0; attempt < 6; attempt++)
        {
            _world.Disconnects.IsDisconnected = true;
            Pass(engine, TimeSpan.FromSeconds(1));
        }

        Assert.Equal(5, _world.Launcher.Opened.Count);
        Assert.Equal(ActivityCode.ReconnectGaveUp, engine.Snapshot.Activity[0].Code);
    }

    [Fact]
    public void Retries_when_roblox_did_not_open_after_an_attempt()
    {
        var engine = CreateRunning(Reconnecting);
        _world.Disconnects.IsDisconnected = true;
        Pass(engine, TimeSpan.FromSeconds(1));

        _world.Windows.Roblox.Clear();
        Pass(engine, TimeSpan.FromSeconds(91));
        Pass(engine, TimeSpan.FromSeconds(31));

        Assert.Equal(2, _world.Launcher.Opened.Count);
    }

    [Fact]
    public void Disconnect_without_reconnect_setting_only_reports()
    {
        var engine = CreateRunning();

        _world.Disconnects.IsDisconnected = true;
        Pass(engine, TimeSpan.FromSeconds(1));

        Assert.Empty(_world.Launcher.Opened);
        Assert.Contains(engine.Snapshot.Activity, entry => entry.Code == ActivityCode.Disconnected);
    }

    [Fact]
    public void Closed_window_is_reported_but_never_reopened()
    {
        var engine = CreateRunning(Reconnecting);
        Pass(engine, TimeSpan.FromSeconds(1));

        _world.Windows.Roblox.Clear();
        Pass(engine, TimeSpan.FromMinutes(5));

        Assert.Empty(_world.Launcher.Opened);
        Assert.Contains(engine.Snapshot.Activity, entry => entry.Code == ActivityCode.RobloxClosed);
    }

    [Fact]
    public void Stopped_app_never_reconnects()
    {
        var engine = CreateEngine(Reconnecting);

        _world.Disconnects.IsDisconnected = true;
        Pass(engine, TimeSpan.FromMinutes(5));

        Assert.Empty(_world.Launcher.Opened);
    }

    [Fact]
    public void Important_events_reach_subscribers()
    {
        var engine = CreateRunning(Reconnecting);
        var seen = new List<ActivityCode>();
        engine.ActivityAdded += entry => seen.Add(entry.Code);

        _world.Disconnects.IsDisconnected = true;
        Pass(engine, TimeSpan.FromSeconds(1));

        Assert.Equal([ActivityCode.Disconnected, ActivityCode.Reconnecting], seen);
    }

    [Theory]
    [InlineData("123456", "roblox://experiences/start?placeId=123456")]
    [InlineData("https://www.roblox.com/games/123456/Some-Game", "roblox://experiences/start?placeId=123456")]
    [InlineData("https://www.roblox.com/ru/games/123456/Some-Game", "roblox://experiences/start?placeId=123456")]
    [InlineData("https://www.roblox.com/games/123456/Game?privateServerLinkCode=abc-1_2", "roblox://experiences/start?placeId=123456&linkCode=abc-1_2")]
    [InlineData("roblox://placeId=123456", "roblox://experiences/start?placeId=123456")]
    [InlineData("  123456 ", "roblox://experiences/start?placeId=123456")]
    public void Game_link_becomes_a_launch_address(string text, string expected) =>
        Assert.Equal(expected, GameLink.ToLaunchUri(text));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("https://www.roblox.com/share?code=abc&type=Server")]
    [InlineData("https://evil.example/games/123")]
    [InlineData("calc.exe")]
    [InlineData("123 & calc.exe")]
    public void Anything_else_is_not_a_game_link(string? text) =>
        Assert.Null(GameLink.ToLaunchUri(text));

    [Fact]
    public void Link_code_cannot_smuggle_extra_parameters()
    {
        var uri = GameLink.ToLaunchUri("https://www.roblox.com/games/1/x?privateServerLinkCode=abc&launchmode=evil");

        Assert.Equal("roblox://experiences/start?placeId=1&linkCode=abc", uri);
    }

    [Fact]
    public void Unknown_reset_interval_turns_reset_off() =>
        Assert.Equal(0, new AppSettings { ResetMinutes = 7 }.Normalized().ResetMinutes);
}
