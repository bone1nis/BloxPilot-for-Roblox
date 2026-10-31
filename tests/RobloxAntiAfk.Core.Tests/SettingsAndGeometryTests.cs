using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Storage;

namespace RobloxAntiAfk.Core.Tests;

public sealed class SettingsAndGeometryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"antiafk-{Guid.NewGuid()}", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(Path.GetDirectoryName(_path)))
        {
            Directory.Delete(Path.GetDirectoryName(_path)!, recursive: true);
        }
    }

    private JsonFileStore<AppSettings> Store() => new(_path, () => new AppSettings());

    [Fact]
    public void Defaults_match_documented_behavior()
    {
        var settings = new AppSettings();

        Assert.Equal(5, settings.IntervalMinutes);
        Assert.Equal(AfkAction.RightClick, settings.Action);
        Assert.Equal(GameKey.Space, settings.Key);
        Assert.Null(settings.ClickPoint);
        Assert.True(settings.AllWindows);
        Assert.True(settings.SkipWhilePlaying);
        Assert.True(settings.KeepAwake);
    }

    [Fact]
    public void Settings_survive_round_trip()
    {
        var settings = new AppSettings { IntervalMinutes = 7, Action = AfkAction.Walk, Key = GameKey.Q, ClickPoint = new WindowPoint(0.1, 0.9), AllWindows = false };

        Store().Save(settings);

        Assert.Equal(settings, Store().Load());
    }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        Assert.Equal(new AppSettings(), Store().Load());
    }

    [Fact]
    public void Corrupted_file_gives_defaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, "{ это не json");

        Assert.Equal(new AppSettings(), Store().Load());
    }

    [Fact]
    public void Old_file_without_new_fields_keeps_defaults_for_them()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, """{ "IntervalMinutes": 3 }""");

        var settings = Store().Load();

        Assert.Equal(3, settings.IntervalMinutes);
        Assert.True(settings.AllWindows);
    }

    [Fact]
    public void Statistics_survive_round_trip_and_old_file_has_no_sessions()
    {
        var store = new JsonFileStore<AfkStatistics>(_path, () => new AfkStatistics());
        var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(3));
        store.Save(new AfkStatistics { TotalActions = 2, Sessions = [new SessionRecord(start, 90, 2)] });

        Assert.Equal([new SessionRecord(start, 90, 2)], store.Load().Sessions);

        File.WriteAllText(_path, """{ "TotalActions": 7 }""");
        Assert.Empty(store.Load().Sessions);
    }

    [Theory]
    [InlineData(-5, AppSettings.MinIntervalMinutes)]
    [InlineData(10, 10)]
    [InlineData(500, AppSettings.MaxIntervalMinutes)]
    public void Interval_is_normalized(int input, int expected)
    {
        Assert.Equal(expected, new AppSettings { IntervalMinutes = input }.Normalized().IntervalMinutes);
    }

    [Fact]
    public void Window_point_converts_between_screen_and_window()
    {
        var window = new ScreenRect(100, 200, 800, 600);

        var point = WindowPoint.FromScreen(new ScreenPoint(300, 350), window);

        Assert.Equal(new WindowPoint(0.25, 0.25), point);
        Assert.Equal(new ScreenPoint(300, 350), point.ToScreen(window));
    }

    [Fact]
    public void Window_point_follows_moved_and_resized_window()
    {
        var point = new WindowPoint(0.5, 0.5);

        Assert.Equal(new ScreenPoint(50, 25), point.ToScreen(new ScreenRect(0, 0, 100, 50)));
    }
}
