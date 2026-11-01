using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using RobloxAntiAfk.App.Presentation;
using RobloxAntiAfk.App.ViewModels;

namespace RobloxAntiAfk.App.Views;

public sealed partial class HotkeyEditor : UserControl
{
    private Window? _window;
    private PhysicalKey _capturedKey;

    public HotkeyEditor()
    {
        InitializeComponent();
        // Перехват на спуске: иначе Tab, пробел и Enter сначала сработают как обычные клавиши интерфейса.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
        ChangeButton.Click += (_, _) => ChangeButton.Focus();
        ChangeButton.LostFocus += (_, _) => Cancel();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is not null)
        {
            _window.Deactivated += OnWindowDeactivated;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_window is not null)
        {
            _window.Deactivated -= OnWindowDeactivated;
            _window = null;
        }

        Cancel();
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => Cancel();

    private void Cancel() => (DataContext as HotkeyEditorViewModel)?.CancelListening();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not HotkeyEditorViewModel { IsListening: true } editor)
        {
            return;
        }

        e.Handled = true;
        if (HotkeyInput.From(e.PhysicalKey, e.KeyModifiers) is { } hotkey)
        {
            _capturedKey = e.PhysicalKey;
            editor.Capture(hotkey);
        }
    }

    /// <summary>Отпущенный пробел иначе нажмёт кнопку и снова начнёт запись.</summary>
    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.PhysicalKey == _capturedKey)
        {
            _capturedKey = PhysicalKey.None;
            e.Handled = true;
        }
    }
}
