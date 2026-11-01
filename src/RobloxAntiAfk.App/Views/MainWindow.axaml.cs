using Avalonia.Controls;

namespace RobloxAntiAfk.App.Views;

public sealed partial class MainWindow : Window
{
    private bool _isSeamless;

    public MainWindow()
    {
        InitializeComponent();
        if (OperatingSystem.IsMacOS())
        {
            UseSeamlessTitleBar();
        }

        RebuildContent();
    }

    public void UseSeamlessTitleBar()
    {
        ExtendClientAreaToDecorationsHint = true;
        _isSeamless = true;
        (Content as MainView)?.UseSeamlessTitleBar();
    }

    /// <summary>Подписи в разметке читаются один раз, поэтому при смене языка экран создаётся заново.</summary>
    public void RebuildContent()
    {
        var view = new MainView();
        if (_isSeamless)
        {
            view.UseSeamlessTitleBar();
        }

        Content = view;
    }
}
