using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace RobloxAntiAfk.App.Views;

/// <summary>Окно поверх игры: застывший кадр с рамкой выбора или подпись с переводом. Клики идут сквозь него.</summary>
public sealed partial class OverlayWindow : Window
{
    private const double PromptMargin = 16;

    private static readonly IBrush NormalText = Brushes.White;
    private static readonly IBrush ErrorText = new SolidColorBrush(Color.Parse("#FFC56B"));

    public OverlayWindow() => InitializeComponent();

    /// <summary>Застывший кадр окна Roblox и подсказка. Рамка появится, когда начнётся выделение.</summary>
    public void ShowFrozen(IImage image, string prompt)
    {
        Frozen.Source = image;
        Frozen.IsVisible = true;
        Dim.IsVisible = true;
        FrameArea.IsVisible = false;
        Card.IsVisible = true;
        Card.HorizontalAlignment = HorizontalAlignment.Center;
        Card.Margin = new Thickness(PromptMargin);
        Message.Text = prompt;
        Message.Foreground = NormalText;
    }

    /// <summary>Рамка в единицах интерфейса относительно окна. Подсказка больше не нужна и не должна закрывать текст.</summary>
    public void ShowFrame(double x, double y, double width, double height)
    {
        Card.IsVisible = false;
        FrameArea.IsVisible = true;
        Canvas.SetLeft(Frame, x);
        Canvas.SetTop(Frame, y);
        Frame.Width = width;
        Frame.Height = height;
    }

    /// <summary>Возвращает высоту подписи при заданной ширине.</summary>
    public double ShowMessage(string text, bool isError, double width)
    {
        Frozen.IsVisible = false;
        Frozen.Source = null;
        Dim.IsVisible = false;
        FrameArea.IsVisible = false;
        Card.IsVisible = true;
        Card.HorizontalAlignment = HorizontalAlignment.Stretch;
        Card.Margin = default;
        Message.Text = text;
        Message.Foreground = isError ? ErrorText : NormalText;
        Card.Measure(new Size(width, double.PositiveInfinity));
        return Math.Ceiling(Card.DesiredSize.Height);
    }

    public void ClearFrozen()
    {
        Frozen.Source = null;
        Frozen.IsVisible = false;
    }
}
