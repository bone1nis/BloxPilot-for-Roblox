using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;
using RobloxAntiAfk.Core.Tests.Fakes;

namespace RobloxAntiAfk.Core.Tests;

public class ScreenTranslatorTests
{
    private static readonly WindowArea Area = new(0.25, 0.5, 0.5, 0.25);
    private static readonly ScreenPoint InsideWindow = new(200, 300);
    private static readonly ScreenPoint LaterInsideWindow = new(600, 500);
    private static readonly RecognizedLine[] Hello = [new("Hello there", 0, 0, 1, 0.5)];

    private sealed class Setup
    {
        public Setup(AppSettings settings, bool isRobloxInFront)
        {
            World = new TestWorld().WithRoblox(TestWorld.First);
            World.Windows.Foreground = isRobloxInFront ? TestWorld.First : TestWorld.OtherApp;
            World.TextReader.Result = new TextReadResult(TextReadStatus.Success, Hello);
            Engine = new AntiAfkEngine(World.Platform, World.Clock, new MemoryStore<AppSettings>(settings), new MemoryStore<AfkStatistics>(new AfkStatistics()));
            Translator = new ScreenTranslator(World.Platform, Engine, Overlay, TranslateAsync, World.Clock);
            Translator.SyncWithSettings();
        }

        public TestWorld World { get; }
        public AntiAfkEngine Engine { get; }
        public FakeOverlay Overlay { get; } = new();
        public ScreenTranslator Translator { get; }
        public List<string> SentForTranslation { get; } = [];
        public string? Translation { get; set; } = "Привет";

        private Task<string?> TranslateAsync(string text, string key, CancellationToken cancellation)
        {
            SentForTranslation.Add(text);
            return Task.FromResult(Translation);
        }
    }

    private static Setup Create(AppSettings? settings = null, bool isRobloxInFront = true) =>
        new(settings ?? new AppSettings { TranslationEnabled = true, TranslationArea = Area, TranslationSelectEachTime = false }, isRobloxInFront);

    [Fact]
    public void Hotkey_is_taken_only_while_translation_is_enabled()
    {
        var setup = Create(new AppSettings());
        Assert.False(setup.Translator.Snapshot.IsHotkeyActive);

        setup.Engine.UpdateSettings(settings => settings with { TranslationEnabled = true });
        setup.Translator.SyncWithSettings();
        Assert.True(setup.Translator.Snapshot.IsHotkeyActive);

        setup.Engine.UpdateSettings(settings => settings with { TranslationEnabled = false });
        setup.Translator.SyncWithSettings();
        Assert.False(setup.Translator.Snapshot.IsHotkeyActive);
        Assert.Equal(TranslationStatus.Disabled, setup.Translator.Snapshot.Status);
    }

    [Fact]
    public void Reads_the_saved_area_of_the_window_and_shows_the_translation()
    {
        var setup = Create();

        setup.Translator.TranslateNow();

        Assert.Equal(new ScreenRect(300, 500, 400, 150), Assert.Single(setup.World.TextReader.Reads));
        Assert.Equal("Привет", setup.Overlay.Text);
        Assert.False(setup.Overlay.IsError);
        Assert.Equal(TranslationStatus.Shown, setup.Translator.Snapshot.Status);
        Assert.Equal("Hello there", setup.Translator.Snapshot.SourceText);
    }

    [Fact]
    public void Hotkey_toggles_the_translation()
    {
        var setup = Create();

        setup.World.TranslationHotkey.Press();
        Assert.Equal("Привет", setup.Overlay.Text);
        Assert.True(setup.Translator.Snapshot.IsOverlayActive);

        setup.World.TranslationHotkey.Press();
        Assert.Null(setup.Overlay.Text);
        Assert.False(setup.Translator.Snapshot.IsOverlayActive);
        Assert.Equal(TranslationStatus.Ready, setup.Translator.Snapshot.Status);
    }

    [Fact]
    public void Hotkey_without_an_area_starts_selection_and_a_second_press_cancels_it()
    {
        var setup = Create(new AppSettings { TranslationEnabled = true });

        setup.World.TranslationHotkey.Press();
        Assert.True(setup.Translator.Snapshot.IsSelecting);
        Assert.True(setup.World.Capture.IsActive);

        setup.World.TranslationHotkey.Press();
        Assert.False(setup.Translator.Snapshot.IsSelecting);
        Assert.False(setup.World.Capture.IsActive);
    }

    [Fact]
    public void Selection_explains_itself_on_screen_and_the_hint_goes_away_on_cancel()
    {
        var setup = Create();

        setup.Translator.BeginSelection();
        Assert.Contains("обведи", setup.Overlay.Text);

        setup.Translator.CancelSelection();
        Assert.Null(setup.Overlay.Text);
    }

    [Fact]
    public void Each_time_mode_cycles_select_translate_hide_and_select_again()
    {
        var setup = Create(new AppSettings { TranslationEnabled = true });
        Assert.Equal(TranslationStatus.ReadyToSelect, setup.Translator.Snapshot.Status);

        setup.World.TranslationHotkey.Press();
        Assert.True(setup.Translator.Snapshot.IsSelecting);
        setup.World.Capture.Drag(InsideWindow, LaterInsideWindow);
        Assert.Equal("Привет", setup.Overlay.Text);
        Assert.False(setup.Translator.Snapshot.IsSelecting);

        setup.World.TranslationHotkey.Press();
        Assert.Null(setup.Overlay.Text);
        Assert.False(setup.Translator.Snapshot.IsSelecting);
        Assert.Equal(TranslationStatus.ReadyToSelect, setup.Translator.Snapshot.Status);

        setup.World.TranslationHotkey.Press();
        Assert.True(setup.Translator.Snapshot.IsSelecting);
        setup.World.Capture.Drag(new ScreenPoint(150, 250), new ScreenPoint(350, 450));

        Assert.Equal("Привет", setup.Overlay.Text);
        Assert.Equal(2, setup.World.TextReader.Reads.Count);
        var second = setup.World.TextReader.Reads[1];
        Assert.Equal(150, second.X, 6);
        Assert.Equal(250, second.Y, 6);
        Assert.Equal(200, second.Width, 6);
        Assert.Equal(200, second.Height, 6);
    }

    [Fact]
    public void Each_time_mode_ignores_the_saved_area_and_second_press_cancels()
    {
        var setup = Create(new AppSettings { TranslationEnabled = true, TranslationArea = Area });

        setup.World.TranslationHotkey.Press();
        Assert.True(setup.Translator.Snapshot.IsSelecting);
        Assert.Empty(setup.World.TextReader.Reads);

        setup.World.TranslationHotkey.Press();
        Assert.False(setup.Translator.Snapshot.IsSelecting);
        Assert.Empty(setup.World.TextReader.Reads);
    }

    [Fact]
    public void Each_time_mode_button_starts_selection()
    {
        var setup = Create(new AppSettings { TranslationEnabled = true });

        setup.Translator.TranslateNow();

        Assert.True(setup.Translator.Snapshot.IsSelecting);
    }

    [Fact]
    public void Each_time_mode_is_on_by_default_even_for_settings_saved_before_it_existed()
    {
        Assert.True(new AppSettings().TranslationSelectEachTime);

        var old = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("""{"TranslationEnabled":true}""");

        Assert.True(old!.TranslationSelectEachTime);
    }

    [Fact]
    public void With_the_mode_off_saved_area_is_read_directly()
    {
        var setup = Create();

        setup.World.TranslationHotkey.Press();

        Assert.False(setup.Translator.Snapshot.IsSelecting);
        Assert.Single(setup.World.TextReader.Reads);
    }

    [Fact]
    public void Dragging_saves_the_area_in_window_fractions_and_translates_right_away()
    {
        var setup = Create(new AppSettings { TranslationEnabled = true });
        setup.Translator.BeginSelection();

        Assert.True(setup.World.Capture.Drag(InsideWindow, LaterInsideWindow));

        var area = setup.Engine.Snapshot.Settings.TranslationArea;
        Assert.NotNull(area);
        Assert.Equal(0.125, area.X, 3);
        Assert.Equal(1.0 / 6, area.Y, 3);
        Assert.Equal(0.5, area.Width, 3);
        Assert.Equal(1.0 / 3, area.Height, 3);
        Assert.Equal("Привет", setup.Overlay.Text);
        Assert.False(setup.Translator.Snapshot.IsSelecting);
    }

    [Fact]
    public void Dragging_backwards_gives_the_same_area()
    {
        var setup = Create(new AppSettings { TranslationEnabled = true });
        setup.Translator.BeginSelection();

        setup.World.Capture.Drag(LaterInsideWindow, InsideWindow);

        Assert.Equal(0.125, setup.Engine.Snapshot.Settings.TranslationArea!.X, 3);
    }

    [Fact]
    public void Tiny_selection_is_rejected_and_keeps_the_old_area()
    {
        var setup = Create();
        setup.Translator.BeginSelection();

        setup.World.Capture.Drag(InsideWindow, new ScreenPoint(InsideWindow.X + 3, InsideWindow.Y + 3));

        Assert.Equal(Area, setup.Engine.Snapshot.Settings.TranslationArea);
        Assert.Equal(TranslationStatus.AreaTooSmall, setup.Translator.Snapshot.Status);
        Assert.Empty(setup.World.TextReader.Reads);
    }

    [Fact]
    public void Clicks_outside_the_roblox_window_pass_through_during_selection()
    {
        var setup = Create(new AppSettings { TranslationEnabled = true });
        setup.Translator.BeginSelection();

        Assert.False(setup.World.Capture.Click(new ScreenPoint(1, 1)));
        Assert.True(setup.Translator.Snapshot.IsSelecting);
    }

    [Fact]
    public void Selection_times_out()
    {
        var setup = Create();
        setup.Translator.BeginSelection();

        setup.World.Clock.Advance(TimeSpan.FromSeconds(31));
        setup.Translator.Tick();

        Assert.False(setup.Translator.Snapshot.IsSelecting);
        Assert.False(setup.World.Capture.IsActive);
    }

    [Fact]
    public void Selection_postpones_the_scheduled_action()
    {
        var setup = Create();
        setup.Engine.ToggleRunning();
        setup.World.Clock.Advance(TimeSpan.FromSeconds(299));
        setup.Translator.BeginSelection();

        setup.World.Clock.Advance(TimeSpan.FromSeconds(5));
        setup.Engine.Tick();

        Assert.Empty(setup.World.Input.Events);
    }

    [Fact]
    public void Does_not_read_the_screen_if_roblox_cannot_be_brought_forward()
    {
        var setup = Create(isRobloxInFront: false);
        setup.World.Windows.RefuseForeground.Add(TestWorld.First);

        setup.Translator.TranslateNow();

        Assert.Empty(setup.World.TextReader.Reads);
        Assert.Empty(setup.SentForTranslation);
        Assert.Equal(TranslationStatus.RobloxNotInFront, setup.Translator.Snapshot.Status);
    }

    [Fact]
    public void Button_in_the_app_brings_roblox_forward_before_reading()
    {
        var setup = Create(isRobloxInFront: false);

        setup.Translator.TranslateNow();

        Assert.Contains("front:1", setup.World.Windows.Log);
        Assert.Single(setup.World.TextReader.Reads);
    }

    [Fact]
    public void Auto_refresh_never_reads_another_program()
    {
        var setup = Create(new AppSettings { TranslationEnabled = true, TranslationArea = Area, TranslationSelectEachTime = false, TranslationAutoRefresh = true });
        setup.Translator.TranslateNow();
        setup.World.Windows.Foreground = TestWorld.OtherApp;

        setup.World.Clock.Advance(TimeSpan.FromSeconds(10));
        setup.Translator.Tick();

        Assert.Single(setup.World.TextReader.Reads);
        Assert.Null(setup.Overlay.Text);
    }

    [Fact]
    public void Panel_comes_back_with_roblox()
    {
        var setup = Create();
        setup.Translator.TranslateNow();
        setup.World.Windows.Foreground = TestWorld.OtherApp;
        setup.Translator.Tick();
        Assert.Null(setup.Overlay.Text);

        setup.World.Windows.Foreground = TestWorld.First;
        setup.Translator.Tick();

        Assert.Equal("Привет", setup.Overlay.Text);
    }

    [Fact]
    public void Auto_refresh_sends_text_only_when_it_changes()
    {
        var setup = Create(new AppSettings { TranslationEnabled = true, TranslationArea = Area, TranslationSelectEachTime = false, TranslationAutoRefresh = true });
        setup.Translator.TranslateNow();

        setup.World.Clock.Advance(TimeSpan.FromSeconds(3));
        setup.Translator.Tick();
        Assert.Equal(2, setup.World.TextReader.Reads.Count);
        Assert.Single(setup.SentForTranslation);

        setup.World.TextReader.Result = new TextReadResult(TextReadStatus.Success, [new RecognizedLine("New text", 0, 0, 1, 0.5)]);
        setup.World.Clock.Advance(TimeSpan.FromSeconds(3));
        setup.Translator.Tick();
        Assert.Equal(["Hello there", "New text"], setup.SentForTranslation);
    }

    [Fact]
    public void Auto_refresh_hides_the_panel_when_the_text_disappears_and_keeps_watching()
    {
        var setup = Create(new AppSettings { TranslationEnabled = true, TranslationArea = Area, TranslationSelectEachTime = false, TranslationAutoRefresh = true });
        setup.Translator.TranslateNow();

        setup.World.TextReader.Result = new TextReadResult(TextReadStatus.Success, []);
        setup.World.Clock.Advance(TimeSpan.FromSeconds(3));
        setup.Translator.Tick();

        Assert.Null(setup.Overlay.Text);
        Assert.True(setup.Translator.Snapshot.IsOverlayActive);
    }

    [Fact]
    public void Without_internet_shows_the_original_text()
    {
        var setup = Create();
        setup.Translation = null;

        setup.Translator.TranslateNow();

        Assert.Contains("Hello there", setup.Overlay.Text);
        Assert.True(setup.Overlay.IsError);
        Assert.Equal(TranslationStatus.ShownOriginal, setup.Translator.Snapshot.Status);
    }

    [Fact]
    public void Empty_area_reports_without_calling_the_translator_and_message_goes_away()
    {
        var setup = Create();
        setup.World.TextReader.Result = new TextReadResult(TextReadStatus.Success, []);

        setup.Translator.TranslateNow();

        Assert.Empty(setup.SentForTranslation);
        Assert.Equal(TranslationStatus.NoText, setup.Translator.Snapshot.Status);
        Assert.NotNull(setup.Overlay.Text);

        setup.World.Clock.Advance(TimeSpan.FromSeconds(5));
        setup.Translator.Tick();
        Assert.Null(setup.Overlay.Text);
    }

    [Theory]
    [InlineData(TextReadStatus.PermissionMissing, TranslationStatus.ScreenPermissionMissing)]
    [InlineData(TextReadStatus.EmptyCapture, TranslationStatus.EmptyCapture)]
    [InlineData(TextReadStatus.Failed, TranslationStatus.ReadFailed)]
    public void Read_problems_are_reported(TextReadStatus read, TranslationStatus expected)
    {
        var setup = Create();
        setup.World.TextReader.Result = TextReadResult.Of(read, "подробности");

        setup.Translator.TranslateNow();

        Assert.Equal(expected, setup.Translator.Snapshot.Status);
        Assert.Equal("подробности", setup.Translator.Snapshot.Detail);
        Assert.Empty(setup.SentForTranslation);
    }

    [Fact]
    public void Missing_screen_permission_asks_for_it_and_does_not_read()
    {
        var setup = Create();
        setup.World.TextReader.IsPermissionGranted = false;

        setup.Translator.TranslateNow();

        Assert.Equal(1, setup.World.TextReader.PermissionRequests);
        Assert.Empty(setup.World.TextReader.Reads);
        Assert.Equal(TranslationStatus.ScreenPermissionMissing, setup.Translator.Snapshot.Status);
    }

    [Fact]
    public void Panel_over_the_area_is_hidden_for_the_snapshot_and_comes_back()
    {
        var setup = Create(new AppSettings { TranslationEnabled = true, TranslationArea = Area, TranslationSelectEachTime = false, TranslationAutoRefresh = true });
        setup.Overlay.PanelToReturn = new ScreenRect(300, 500, 100, 100);
        setup.Translator.TranslateNow();

        setup.World.Clock.Advance(TimeSpan.FromSeconds(3));
        setup.Translator.Tick();

        Assert.Equal(1, setup.Overlay.Hides);
        Assert.Equal(2, setup.World.TextReader.Reads.Count);
        Assert.Equal("Привет", setup.Overlay.Text);
    }

    [Fact]
    public void Panel_beside_the_area_is_not_hidden_for_the_snapshot()
    {
        var setup = Create(new AppSettings { TranslationEnabled = true, TranslationArea = Area, TranslationSelectEachTime = false, TranslationAutoRefresh = true });
        setup.Overlay.PanelToReturn = new ScreenRect(300, 660, 100, 40);
        setup.Translator.TranslateNow();

        setup.World.Clock.Advance(TimeSpan.FromSeconds(3));
        setup.Translator.Tick();

        Assert.Equal(0, setup.Overlay.Hides);
    }

    [Fact]
    public void Roblox_missing_is_reported_when_selecting()
    {
        var setup = Create();
        setup.World.Windows.Roblox.Clear();

        setup.Translator.BeginSelection();

        Assert.Equal(TranslationStatus.RobloxMissing, setup.Translator.Snapshot.Status);
        Assert.False(setup.World.Capture.IsActive);
    }

    [Fact]
    public void Reset_keeps_area_and_key_but_turns_translation_off()
    {
        var setup = Create(new AppSettings { TranslationEnabled = true, TranslationArea = Area, TranslationSelectEachTime = false, DeepLKey = "key:fx", TranslationAutoRefresh = true });

        setup.Engine.ResetSettings();

        var settings = setup.Engine.Snapshot.Settings;
        Assert.False(settings.TranslationEnabled);
        Assert.False(settings.TranslationAutoRefresh);
        Assert.Equal(Area, settings.TranslationArea);
        Assert.Equal("key:fx", settings.DeepLKey);
    }

    [Fact]
    public void Translation_is_off_by_default() => Assert.False(new AppSettings().TranslationEnabled);
}
