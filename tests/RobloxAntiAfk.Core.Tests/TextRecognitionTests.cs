using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Ocr;
using RobloxAntiAfk.Platform.Mac;
using SkiaSharp;

namespace RobloxAntiAfk.Core.Tests;

/// <summary>
/// Настоящие распознаватели на картинках, нарисованных прямо в тесте. Экран не снимается:
/// проверяем, что модели загружаются и читают надписи вроде игровых.
/// </summary>
public class TextRecognitionTests
{
    private static readonly RapidTextRecognizer Rapid = new(Path.Combine(Path.GetTempPath(), "roblox-antiafk-test-models"));

    public static TheoryData<string, string[], string[]> Samples => new()
    {
        { "тёмная панель диалога", ["Talk to the merchant to", "buy a new sword"], ["merchant", "sword"] },
        { "мелкий текст квеста", ["Quest: collect 10 coins", "Reward: 500 gold"], ["collect", "Reward"] },
        { "крупная кнопка", ["PLAY NOW"], ["PLAY"] },
    };

    private static SKBitmap Draw(string[] lines, int fontSize, SKColor background, SKColor foreground)
    {
        var bitmap = new SKBitmap(760, 40 + lines.Length * (fontSize + 14), SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(background);
        using var font = new SKFont(SKTypeface.FromFamilyName(null, SKFontStyle.Bold), fontSize);
        using var paint = new SKPaint { Color = foreground, IsAntialias = true };
        for (var index = 0; index < lines.Length; index++)
        {
            canvas.DrawText(lines[index], 24, 24 + fontSize + index * (fontSize + 14), SKTextAlign.Left, font, paint);
        }

        return bitmap;
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Windows_recognizer_reads_game_like_text(string name, string[] lines, string[] expected)
    {
        Assert.False(string.IsNullOrEmpty(name));
        using var bitmap = Draw(lines, 24, new SKColor(24, 28, 38), SKColors.White);
        var image = new PixelImage(bitmap.Width, bitmap.Height, bitmap.Bytes);

        var result = Rapid.Recognize(image, TestContext.Current.CancellationToken);

        var text = string.Join(' ', result.Select(line => line.Text));
        Assert.All(expected, word => Assert.Contains(word, text, StringComparison.OrdinalIgnoreCase));
        Assert.All(result, line => Assert.InRange(line.Y, 0, 1));
    }

    [Fact]
    public void Windows_recognizer_returns_nothing_for_an_empty_picture()
    {
        using var bitmap = Draw([], 24, new SKColor(24, 28, 38), SKColors.White);
        var image = new PixelImage(bitmap.Width, bitmap.Height, bitmap.Bytes);

        Assert.Empty(Rapid.Recognize(image, TestContext.Current.CancellationToken));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Mac_vision_reads_game_like_text(string name, string[] lines, string[] expected)
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "Проверка только для macOS");
        Assert.False(string.IsNullOrEmpty(name));
        var path = Path.Combine(Path.GetTempPath(), $"roblox-antiafk-test-{Guid.NewGuid():N}.png");
        try
        {
            using var bitmap = Draw(lines, 24, new SKColor(24, 28, 38), SKColors.White);
            using (var output = File.Create(path))
            {
                bitmap.Encode(output, SKEncodedImageFormat.Png, 100);
            }

            var result = MacVision.Recognize(path);

            var text = string.Join(' ', result.Select(line => line.Text));
            Assert.All(expected, word => Assert.Contains(word, text, StringComparison.OrdinalIgnoreCase));
            Assert.All(result, line => Assert.InRange(line.Y, 0, 1));
            if (lines.Length > 1)
            {
                Assert.True(result[0].Y < result[^1].Y, "Строки должны идти сверху вниз");
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>В застывшем кадре читается только выделенная часть.</summary>
    [Fact]
    public void Mac_vision_reads_only_the_requested_region()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "Проверка только для macOS");
        var path = Path.Combine(Path.GetTempPath(), $"roblox-antiafk-test-{Guid.NewGuid():N}.png");
        try
        {
            using var bitmap = Draw(["Talk to the merchant", "", "", "", "Collect golden coins"], 24, new SKColor(24, 28, 38), SKColors.White);
            using (var output = File.Create(path))
            {
                bitmap.Encode(output, SKEncodedImageFormat.Png, 100);
            }

            var bottom = string.Join(' ', MacVision.Recognize(path, new WindowArea(0, 0.6, 1, 0.4)).Select(line => line.Text));

            Assert.Contains("Collect", bottom, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("merchant", bottom, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Windows_recognizer_reads_a_cropped_part_of_a_frame()
    {
        using var bitmap = Draw(["Talk to the merchant", "", "", "", "Collect golden coins"], 24, new SKColor(24, 28, 38), SKColors.White);
        var frame = new PixelImage(bitmap.Width, bitmap.Height, bitmap.Bytes);

        var result = Rapid.Recognize(frame.Crop(new WindowArea(0, 0.6, 1, 0.4)), TestContext.Current.CancellationToken);

        var text = string.Join(' ', result.Select(line => line.Text));
        Assert.Contains("Collect", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("merchant", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Screen_capture_arguments_are_silent_and_rounded() =>
        Assert.Equal(
            ["-x", "-t", "png", "-R", "11,20,300,41", "/tmp/a.png"],
            MacScreenTextReader.CaptureArguments(new ScreenRect(10.6, 20.2, 300.4, 40.6), "/tmp/a.png"));
}
