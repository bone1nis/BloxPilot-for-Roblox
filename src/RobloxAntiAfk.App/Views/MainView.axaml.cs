using Avalonia;
using Avalonia.Controls;

namespace RobloxAntiAfk.App.Views;

public sealed partial class MainView : UserControl
{
    public MainView() => InitializeComponent();

    /// <summary>На Mac содержимое заходит под прозрачный заголовок, поэтому сверху нужен отступ.</summary>
    public void UseSeamlessTitleBar()
    {
        ContentArea.Margin = new Thickness(28, 44, 0, 0);
        StatusArea.UseTitleBarInset();
    }
}
