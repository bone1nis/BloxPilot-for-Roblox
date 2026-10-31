using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using RobloxAntiAfk.App.Views;
using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;

namespace RobloxAntiAfk.App.Presentation;

/// <summary>
/// Переводит координаты экрана в размеры окна. Windows считает позицию в настоящих пикселях, а размер
/// окна в единицах интерфейса, поэтому нужен масштаб. На Mac и то и другое уже в точках экрана.
/// </summary>
public sealed class TranslationOverlay(IOverlaySupport support, bool isMac) : ITranslationOverlay
{
    private const double PanelWidth = 420;
    private const double PanelGap = 8;

    private OverlayWindow? _window;
    private Bitmap? _frozen;
    private ScreenRect _frozenBounds;

    public nint Handle => _window?.TryGetPlatformHandle()?.Handle ?? 0;

    public void ShowFrozen(ScreenSnapshot snapshot, string prompt)
    {
        var window = _window ??= new OverlayWindow();
        ReleaseFrozen(window);
        _frozen = ToBitmap(snapshot);
        _frozenBounds = snapshot.Bounds;
        window.ShowFrozen(_frozen, prompt);
        Place(window, snapshot.Bounds, ScaleAt(window, snapshot.Bounds), IsFullScreen(window, snapshot.Bounds));
    }

    public void ShowSelection(ScreenRect area)
    {
        if (_window is not { } window)
        {
            return;
        }

        var scale = ScaleAt(window, _frozenBounds);
        window.ShowFrame((area.X - _frozenBounds.X) / scale, (area.Y - _frozenBounds.Y) / scale, area.Width / scale, area.Height / scale);
    }

    public ScreenRect ShowText(ScreenRect area, ScreenRect robloxWindow, string text, bool isError)
    {
        var window = _window ??= new OverlayWindow();
        ReleaseFrozen(window);
        var scale = ScaleAt(window, area);
        var width = Math.Min(Math.Max(area.Width, PanelWidth * scale), robloxWindow.Width);
        var height = window.ShowMessage(text, isError, width / scale) * scale;
        var panel = TextLayout.PlacePanel(area, robloxWindow, width, height, PanelGap * scale);
        Place(window, panel, scale, IsFullScreen(window, robloxWindow));
        return panel;
    }

    public void Hide()
    {
        if (_window is { } window)
        {
            window.Hide();
            ReleaseFrozen(window);
        }

        support.Release();
    }

    private void ReleaseFrozen(OverlayWindow window)
    {
        window.ClearFrozen();
        _frozen?.Dispose();
        _frozen = null;
    }

    /// <summary>На Windows снимок уже в памяти, а на Mac его сохраняет системная утилита.</summary>
    private static Bitmap ToBitmap(ScreenSnapshot snapshot)
    {
        if (snapshot.Pixels is not { } image)
        {
            return new Bitmap(snapshot.ImagePath ?? throw new InvalidOperationException("В снимке нет изображения."));
        }

        var bitmap = new WriteableBitmap(new PixelSize(image.Width, image.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var target = bitmap.Lock();
        Marshal.Copy(image.Bgra, 0, target.Address, image.Bgra.Length);
        return bitmap;
    }

    private void Place(OverlayWindow window, ScreenRect rect, double scale, bool aboveFullScreen)
    {
        window.Position = new PixelPoint((int)Math.Round(rect.X), (int)Math.Round(rect.Y));
        window.Width = Math.Max(1, rect.Width / scale);
        window.Height = Math.Max(1, rect.Height / scale);
        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.TryGetPlatformHandle() is { } handle)
        {
            support.Prepare(handle.Handle, aboveFullScreen);
        }
    }

    private double ScaleAt(OverlayWindow window, ScreenRect area) =>
        isMac ? 1 : window.Screens.ScreenFromPoint(new PixelPoint((int)area.X, (int)area.Y))?.Scaling ?? 1;

    private bool IsFullScreen(OverlayWindow window, ScreenRect robloxWindow)
    {
        if (!isMac || window.Screens.ScreenFromPoint(new PixelPoint((int)robloxWindow.X, (int)robloxWindow.Y)) is not { } screen)
        {
            return false;
        }

        return robloxWindow.Width >= screen.Bounds.Width / screen.Scaling - 1 && robloxWindow.Height >= screen.Bounds.Height / screen.Scaling - 1
            || robloxWindow.Width >= screen.Bounds.Width - 1 && robloxWindow.Height >= screen.Bounds.Height - 1;
    }
}
