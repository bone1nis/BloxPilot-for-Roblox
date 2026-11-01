using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Localization;

namespace RobloxAntiAfk.Platform.Windows;

/// <summary>Снимает часть экрана обычным копированием пикселей: процесс Roblox при этом не затрагивается.</summary>
internal sealed unsafe class WindowsScreenTextReader(ITextRecognizer? recognizer) : IScreenTextReader
{
    public bool IsPermissionGranted => true;

    public void RequestPermission()
    {
    }

    public void OpenPermissionSettings()
    {
    }

    public Task<TextReadResult> ReadAsync(ScreenRect area, CancellationToken cancellation) =>
        Task.Run(() => Recognize(() => Capture(area), cancellation), CancellationToken.None);

    public Task<TextReadResult> ReadAsync(ScreenSnapshot snapshot, ScreenRect area, CancellationToken cancellation) =>
        Task.Run(
            () => Recognize(
                () => (snapshot.Pixels ?? throw new InvalidOperationException("В снимке нет изображения.")).Crop(WindowArea.FromScreen(area, snapshot.Bounds)),
                cancellation),
            CancellationToken.None);

    public Task<SnapshotResult> CaptureAsync(ScreenRect area, CancellationToken cancellation) => Task.Run(() =>
    {
        try
        {
            var image = Capture(area);
            return IsSingleColor(image)
                ? new SnapshotResult(TextReadStatus.EmptyCapture, null)
                : new SnapshotResult(TextReadStatus.Success, new ScreenSnapshot(area, image, null));
        }
        catch (Exception exception)
        {
            return new SnapshotResult(TextReadStatus.Failed, null, $"{exception.GetType().Name}: {exception.Message}");
        }
    }, CancellationToken.None);

    private TextReadResult Recognize(Func<PixelImage> getImage, CancellationToken cancellation)
    {
        if (recognizer is null)
        {
            return TextReadResult.Of(TextReadStatus.Failed, L.T("Распознавание не входит в эту сборку."));
        }

        try
        {
            var image = getImage();
            return IsSingleColor(image)
                ? TextReadResult.Of(TextReadStatus.EmptyCapture)
                : new TextReadResult(TextReadStatus.Success, recognizer.Recognize(image, cancellation));
        }
        catch (OperationCanceledException)
        {
            return new TextReadResult(TextReadStatus.Success, []);
        }
        catch (Exception exception)
        {
            return TextReadResult.Of(TextReadStatus.Failed, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    internal static PixelImage Capture(ScreenRect area)
    {
        var x = (int)Math.Round(area.X);
        var y = (int)Math.Round(area.Y);
        var width = Math.Max(1, (int)Math.Round(area.Width));
        var height = Math.Max(1, (int)Math.Round(area.Height));
        var pixels = new byte[width * height * 4];

        var screen = NativeMethods.GetDC(0);
        var memory = NativeMethods.CreateCompatibleDC(screen);
        var bitmap = NativeMethods.CreateCompatibleBitmap(screen, width, height);
        var previous = NativeMethods.SelectObject(memory, bitmap);
        try
        {
            if (NativeMethods.BitBlt(memory, 0, 0, width, height, screen, x, y, NativeMethods.RasterCopy | NativeMethods.RasterCaptureLayered) == 0)
            {
                throw new InvalidOperationException("Система не отдала снимок экрана.");
            }

            // Отрицательная высота означает, что строки идут сверху вниз.
            var info = new BitmapInfoHeader
            {
                Size = (uint)sizeof(BitmapInfoHeader),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
            };

            fixed (byte* target = pixels)
            {
                // Функция дописывает данные за заголовком только для палитры, а её у 32 бит нет.
                if (NativeMethods.GetDIBits(memory, bitmap, 0, (uint)height, target, &info, 0) == 0)
                {
                    throw new InvalidOperationException("Не удалось прочитать снимок экрана.");
                }
            }
        }
        finally
        {
            NativeMethods.SelectObject(memory, previous);
            NativeMethods.DeleteObject(bitmap);
            NativeMethods.DeleteDC(memory);
            NativeMethods.ReleaseDC(0, screen);
        }

        return new PixelImage(width, height, pixels);
    }

    internal static bool IsSingleColor(PixelImage image)
    {
        var pixels = image.Bgra;
        for (var index = 4; index + 2 < pixels.Length; index += 4)
        {
            if (pixels[index] != pixels[0] || pixels[index + 1] != pixels[1] || pixels[index + 2] != pixels[2])
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>Флаги те же, что у подсветки курсора в PowerToys: окно видно, но для мыши его будто нет.</summary>
internal sealed class WindowsOverlaySupport : IOverlaySupport
{
    public void Prepare(nint handle, bool aboveFullScreen)
    {
        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.ExtendedStyleIndex);
        NativeMethods.SetWindowLongPtr(
            handle,
            NativeMethods.ExtendedStyleIndex,
            style | NativeMethods.StyleLayered | NativeMethods.StyleTransparent | NativeMethods.StyleNoActivate | NativeMethods.StyleToolWindow);
        NativeMethods.SetLayeredWindowAttributes(handle, 0, 255, NativeMethods.LayeredUseAlpha);
    }

    public void Release()
    {
    }

    public bool IsClickThrough(nint handle, ScreenPoint point) =>
        NativeMethods.WindowFromPoint(new NativePoint { X = (int)point.X, Y = (int)point.Y }) != handle;
}
