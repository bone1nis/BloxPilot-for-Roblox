using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace RobloxAntiAfk.App.Views;

/// <summary>Окно поверх игры: рамка выбора области или подпись с переводом. Клики идут сквозь него.</summary>
public sealed partial class OverlayWindow : Window
{
    private static readonly IBrush NormalText = Brushes.White;
    private static readonly IBrush ErrorText = new SolidColorBrush(Color.Parse("#FFC56B"));

    public OverlayWindow() => InitializeComponent();

    public void ShowFrame()
    {
        Card.IsVisible = false;
        Frame.IsVisible = true;
    }

    /// <summary>Возвращает высоту подписи при заданной ширине.</summary>
    public double ShowMessage(string text, bool isError, double width)
    {
        Frame.IsVisible = false;
        Card.IsVisible = true;
        Message.Text = text;
        Message.Foreground = isError ? ErrorText : NormalText;
        Card.Measure(new Size(width, double.PositiveInfinity));
        return Math.Ceiling(Card.DesiredSize.Height);
    }
}
