using Avalonia;
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

    public nint Handle => _window?.TryGetPlatformHandle()?.Handle ?? 0;

    public void ShowSelection(ScreenRect area)
    {
        var window = _window ??= new OverlayWindow();
        window.ShowFrame();
        Place(window, area, ScaleAt(window, area), aboveFullScreen: false);
    }

    public ScreenRect ShowText(ScreenRect area, ScreenRect robloxWindow, string text, bool isError)
    {
        var window = _window ??= new OverlayWindow();
        var scale = ScaleAt(window, area);
        var width = Math.Min(Math.Max(area.Width, PanelWidth * scale), robloxWindow.Width);
        var height = window.ShowMessage(text, isError, width / scale) * scale;
        var panel = TextLayout.PlacePanel(area, robloxWindow, width, height, PanelGap * scale);
        Place(window, panel, scale, IsFullScreen(window, robloxWindow));
        return panel;
    }

    public void Hide()
    {
        _window?.Hide();
        support.Release();
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
