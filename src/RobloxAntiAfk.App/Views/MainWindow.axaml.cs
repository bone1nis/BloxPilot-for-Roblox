using Avalonia;
using Avalonia.Controls;

namespace RobloxAntiAfk.App.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        if (OperatingSystem.IsMacOS())
        {
            UseSeamlessTitleBar();
        }
    }

    /// <summary>На Mac содержимое заходит под прозрачный заголовок, поэтому нужно место под кнопки окна.</summary>
    public void UseSeamlessTitleBar()
    {
        ExtendClientAreaToDecorationsHint = true;
        ContentArea.Margin = new Thickness(28, 44, 0, 0);
        StatusArea.UseTitleBarInset();
    }
}
