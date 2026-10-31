using Avalonia;
using Avalonia.Controls;

namespace RobloxAntiAfk.App.Views;

public sealed partial class StatusPanel : UserControl
{
    public StatusPanel() => InitializeComponent();

    public void UseTitleBarInset() => Root.Padding = new Thickness(24, 44, 24, 24);
}
