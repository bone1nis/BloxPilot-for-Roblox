using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;

namespace RobloxAntiAfk.Core.Tests;

public class TextLayoutTests
{
    private static readonly ScreenRect Window = new(100, 100, 800, 600);

    [Fact]
    public void Wrapped_lines_become_one_sentence()
    {
        RecognizedLine[] lines = [new("Talk to the merchant", 0.1, 0.1, 0.6, 0.1), new("to buy a new sword", 0.1, 0.23, 0.5, 0.1)];

        Assert.Equal("Talk to the merchant to buy a new sword", TextLayout.ToText(lines));
    }

    [Fact]
    public void Lines_far_apart_stay_separate()
    {
        RecognizedLine[] lines = [new("Quest log", 0.1, 0.05, 0.3, 0.08), new("Collect coins", 0.1, 0.6, 0.3, 0.08)];

        Assert.Equal("Quest log\nCollect coins", TextLayout.ToText(lines));
    }

    [Fact]
    public void Lines_on_one_row_read_left_to_right_as_separate_labels()
    {
        RecognizedLine[] lines = [new("Sell", 0.6, 0.11, 0.2, 0.1), new("Buy", 0.1, 0.1, 0.2, 0.1)];

        Assert.Equal("Buy\nSell", TextLayout.ToText(lines));
    }

    [Fact]
    public void Order_of_recognizer_results_does_not_matter()
    {
        RecognizedLine[] lines = [new("second line", 0.1, 0.23, 0.5, 0.1), new("first line", 0.1, 0.1, 0.5, 0.1)];

        Assert.Equal("first line second line", TextLayout.ToText(lines));
    }

    [Fact]
    public void Scraps_without_letters_are_dropped()
    {
        RecognizedLine[] lines = [new("1,250", 0.1, 0.1, 0.1, 0.1), new("x", 0.1, 0.4, 0.1, 0.1), new("Gold", 0.1, 0.7, 0.1, 0.1)];

        Assert.Equal("Gold", TextLayout.ToText(lines));
    }

    [Fact]
    public void Nothing_recognized_gives_empty_text() => Assert.Equal("", TextLayout.ToText([]));

    [Fact]
    public void Panel_goes_below_the_area_when_it_fits()
    {
        var panel = TextLayout.PlacePanel(new ScreenRect(200, 200, 300, 50), Window, 420, 80, 8);

        Assert.Equal(new ScreenRect(200, 258, 420, 80), panel);
        Assert.False(TextLayout.Overlaps(panel, new ScreenRect(200, 200, 300, 50)));
    }

    [Fact]
    public void Panel_goes_above_when_the_area_is_at_the_bottom()
    {
        var panel = TextLayout.PlacePanel(new ScreenRect(200, 620, 300, 70), Window, 420, 80, 8);

        Assert.Equal(532, panel.Y);
    }

    [Fact]
    public void Panel_stays_inside_the_window_and_covers_a_huge_area_only_when_it_must()
    {
        var area = new ScreenRect(100, 100, 800, 600);

        var panel = TextLayout.PlacePanel(area, Window, 420, 80, 8);

        Assert.Equal(620, panel.Y);
        Assert.True(TextLayout.Overlaps(panel, area));
    }

    [Fact]
    public void Panel_is_shifted_left_to_stay_in_the_window()
    {
        var panel = TextLayout.PlacePanel(new ScreenRect(800, 200, 90, 50), Window, 420, 80, 8);

        Assert.Equal(480, panel.X);
    }

    [Fact]
    public void Panel_never_exceeds_a_small_window()
    {
        var panel = TextLayout.PlacePanel(new ScreenRect(0, 0, 50, 50), new ScreenRect(0, 0, 300, 100), 420, 500, 8);

        Assert.Equal(300, panel.Width);
        Assert.Equal(100, panel.Height);
    }

    [Fact]
    public void Area_keeps_its_place_when_the_window_moves()
    {
        var area = WindowArea.FromScreen(new ScreenRect(200, 400, 400, 150), new ScreenRect(100, 100, 800, 600));

        Assert.Equal(new WindowArea(0.125, 0.5, 0.5, 0.25), area);
        Assert.Equal(new ScreenRect(150, 350, 400, 150), area.ToScreen(new ScreenRect(50, 50, 800, 600)));
    }

    [Fact]
    public void Area_outside_the_window_is_cut_to_it()
    {
        var area = WindowArea.FromScreen(new ScreenRect(0, 0, 500, 300), new ScreenRect(100, 100, 800, 600));

        Assert.Equal(0, area.X);
        Assert.Equal(0, area.Y);
        Assert.Equal(0.5, area.Width);
    }

    [Fact]
    public void Broken_saved_area_is_fixed_when_settings_load()
    {
        var settings = new AppSettings { TranslationArea = new WindowArea(0.9, -1, 5, 5) }.Normalized();

        Assert.Equal(new WindowArea(0.9, 0, 1 - 0.9, 1), settings.TranslationArea);
    }
}
