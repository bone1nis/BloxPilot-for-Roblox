#if WINDOWS_OCR
using RobloxAntiAfk.App.Presentation;
using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Ocr;
using SkiaSharp;

namespace RobloxAntiAfk.App;

/// <summary>
/// Проверка перевода в собранном exe на настоящей Windows, её запускает CI. Roblox не нужен.
/// Сбой распознавания считается ошибкой. Снимок и подпись зависят от рабочего стола машины, поэтому их сбой только предупреждение.
/// </summary>
internal static class SelfTest
{
    private const string Sample = "Talk to the merchant";

    public static async Task<int> RunAsync(PlatformServices platform, string reportPath)
    {
        var report = new List<string>();
        var exitCode = 0;
        try
        {
            using var recognizer = new RapidTextRecognizer();
            var text = string.Join(' ', recognizer.Recognize(DrawSample(), CancellationToken.None).Select(line => line.Text));
            var isRead = text.Contains("merchant", StringComparison.OrdinalIgnoreCase);
            report.Add($"{(isRead ? "OK" : "FAIL")} распознавание картинки: '{text}'");
            exitCode = isRead ? 0 : 1;
        }
        catch (Exception exception)
        {
            report.Add($"FAIL распознавание картинки: {exception}");
            exitCode = 1;
        }

        try
        {
            var overlay = new TranslationOverlay(platform.Overlay, isMac: false);
            var panel = overlay.ShowText(new ScreenRect(150, 130, 620, 40), new ScreenRect(100, 100, 800, 500), Sample, isError: false);
            await Task.Delay(TimeSpan.FromSeconds(2));

            var read = await platform.TextReader.ReadAsync(panel, CancellationToken.None);
            var captured = string.Join(' ', read.Lines.Select(line => line.Text));
            report.Add($"{(captured.Contains("merchant", StringComparison.OrdinalIgnoreCase) ? "OK" : "WARN")} снимок подписи: {read.Status} '{captured}' {read.Detail}");
            report.Add($"{(platform.Overlay.IsClickThrough(overlay.Handle, panel.Center) ? "OK" : "WARN")} клики идут сквозь подпись");
            overlay.Hide();
        }
        catch (Exception exception)
        {
            report.Add($"WARN подпись и снимок: {exception}");
        }

        await File.WriteAllLinesAsync(reportPath, report);
        return exitCode;
    }

    private static PixelImage DrawSample()
    {
        using var bitmap = new SKBitmap(640, 80, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(24, 28, 38));
        using var font = new SKFont(SKTypeface.Default, 30);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawText(Sample, 20, 50, SKTextAlign.Left, font, paint);
        return new PixelImage(bitmap.Width, bitmap.Height, bitmap.Bytes);
    }
}
#endif
