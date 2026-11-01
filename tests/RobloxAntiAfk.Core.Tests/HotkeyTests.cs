using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;
using RobloxAntiAfk.Core.Storage;
using RobloxAntiAfk.Core.Tests.Fakes;
using RobloxAntiAfk.Platform.Mac;
using RobloxAntiAfk.Platform.Windows;

namespace RobloxAntiAfk.Core.Tests;

public class HotkeyTests
{
    private static readonly Hotkey SingleKey = new("R");
    private static readonly Hotkey Combination = new("Digit5", Control: true, Alt: true);

    private readonly TestWorld _world = new TestWorld().WithRoblox(TestWorld.First);

    private AntiAfkEngine CreateEngine(AppSettings? settings = null) => new(
        _world.Platform,
        _world.Clock,
        new MemoryStore<AppSettings>(settings ?? new AppSettings()),
        new MemoryStore<AfkStatistics>(new AfkStatistics()));

    private ScreenTranslator CreateTranslator(AntiAfkEngine engine)
    {
        var translator = new ScreenTranslator(
            _world.Platform,
            engine,
            new FakeOverlay(),
            (_, _, _) => Task.FromResult<string?>("Привет"),
            _world.Clock);
        translator.SyncWithSettings();
        return translator;
    }

    public static TheoryData<string> CommonKeys
    {
        get
        {
            var keys = new TheoryData<string> { "Space", "Enter", "Tab", "Backspace", "ArrowUp", "Minus", Hotkey.EscapeKey };
            for (var letter = 'A'; letter <= 'Z'; letter++)
            {
                keys.Add(letter.ToString());
            }

            for (var digit = 0; digit <= 9; digit++)
            {
                keys.Add($"Digit{digit}");
                keys.Add($"NumPad{digit}");
            }

            for (var function = 1; function <= 12; function++)
            {
                keys.Add($"F{function}");
            }

            return keys;
        }
    }

    [Theory]
    [MemberData(nameof(CommonKeys))]
    public void Common_keys_work_on_both_systems(string key)
    {
        Assert.True(WindowsHotkey.VirtualKeys.ContainsKey(key));
        Assert.True(MacHotkey.KeyCodes.ContainsKey(key));
    }

    [Fact]
    public void Key_codes_are_unique()
    {
        Assert.Equal(WindowsHotkey.VirtualKeys.Count, WindowsHotkey.VirtualKeys.Values.Distinct().Count());
        Assert.Equal(MacHotkey.KeyCodes.Count, MacHotkey.KeyCodes.Values.Distinct().Count());
    }

    [Fact]
    public void Labels_use_the_names_of_each_system()
    {
        var hotkey = new Hotkey("Digit5", Control: true, Shift: true, Alt: true, Meta: true);

        Assert.Equal("Ctrl+Alt+Shift+Win+5", hotkey.ToLabel(isMac: false));
        Assert.Equal("Control+Option+Shift+Command+5", hotkey.ToLabel(isMac: true));
        Assert.Equal("Esc", new Hotkey(Hotkey.EscapeKey).ToLabel(isMac: true));
        Assert.Equal("Пробел", new Hotkey("Space").ToLabel(isMac: false));
    }

    [Fact]
    public void Chosen_hotkeys_survive_saving_and_old_settings_have_none()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var store = new JsonFileStore<AppSettings>(path, () => new AppSettings());
            Assert.Null(store.Load().ToggleHotkey);

            var settings = new AppSettings { ToggleHotkey = SingleKey, TranslationHotkey = Combination };
            store.Save(settings);

            Assert.Equal(settings, new JsonFileStore<AppSettings>(path, () => new AppSettings()).Load());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Default_settings_reset_drops_chosen_hotkeys()
    {
        var settings = new AppSettings { ToggleHotkey = SingleKey, TranslationHotkey = Combination };

        var reset = settings.ToDefaultsKeepingUserData();

        Assert.Null(reset.ToggleHotkey);
        Assert.Null(reset.TranslationHotkey);
    }

    [Fact]
    public void Engine_takes_the_saved_hotkey_on_start_and_rebinds_on_change()
    {
        var engine = CreateEngine(new AppSettings { ToggleHotkey = SingleKey });

        engine.Start();
        Assert.Equal(SingleKey, _world.Hotkey.Bound);
        Assert.True(_world.Hotkey.IsStarted);
        Assert.Equal("R", engine.Snapshot.HotkeyLabel);

        engine.UpdateSettings(settings => settings with { ToggleHotkey = Combination });
        Assert.Equal(Combination, _world.Hotkey.Bound);
        Assert.True(_world.Hotkey.IsStarted);

        engine.UpdateSettings(settings => settings with { ToggleHotkey = null });
        Assert.Null(_world.Hotkey.Bound);
        Assert.Equal("Ctrl+Shift+F8", engine.Snapshot.HotkeyLabel);
    }

    [Fact]
    public void Busy_hotkey_is_reported_and_a_free_one_recovers()
    {
        var engine = CreateEngine();
        _world.Hotkey.CanStart = false;
        engine.Start();
        Assert.False(engine.Snapshot.IsHotkeyActive);

        _world.Hotkey.CanStart = true;
        engine.UpdateSettings(settings => settings with { ToggleHotkey = Combination });

        Assert.True(engine.Snapshot.IsHotkeyActive);
    }

    [Fact]
    public void Pausing_releases_both_hotkeys_and_resuming_applies_the_new_one()
    {
        var engine = CreateEngine(new AppSettings { TranslationEnabled = true });
        var translator = CreateTranslator(engine);
        engine.Changed += translator.SyncWithSettings;
        engine.Start();
        Assert.True(_world.Hotkey.IsStarted);
        Assert.True(_world.TranslationHotkey.IsStarted);

        engine.PauseHotkeys(true);
        Assert.False(_world.Hotkey.IsStarted);
        Assert.False(_world.TranslationHotkey.IsStarted);

        engine.UpdateSettings(settings => settings with { TranslationHotkey = Combination });
        Assert.False(_world.TranslationHotkey.IsStarted);

        engine.PauseHotkeys(false);
        Assert.True(_world.Hotkey.IsStarted);
        Assert.True(_world.TranslationHotkey.IsStarted);
        Assert.Equal(Combination, _world.TranslationHotkey.Bound);
        Assert.Equal("Ctrl+Alt+5", translator.HotkeyLabel);
    }

    [Fact]
    public void Translator_label_follows_the_saved_hotkey_even_while_translation_is_off()
    {
        var engine = CreateEngine(new AppSettings { TranslationHotkey = Combination });

        var translator = CreateTranslator(engine);

        Assert.Equal("Ctrl+Alt+5", translator.HotkeyLabel);
        Assert.False(_world.TranslationHotkey.IsStarted);
    }

    [Fact]
    public void Changing_the_translation_hotkey_rebinds_it()
    {
        var engine = CreateEngine(new AppSettings { TranslationEnabled = true });
        var translator = CreateTranslator(engine);
        Assert.True(_world.TranslationHotkey.IsStarted);

        engine.UpdateSettings(settings => settings with { TranslationHotkey = SingleKey });
        translator.SyncWithSettings();

        Assert.Equal(SingleKey, _world.TranslationHotkey.Bound);
        Assert.True(_world.TranslationHotkey.IsStarted);
        Assert.True(translator.Snapshot.IsHotkeyActive);
    }
}
