using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;

namespace RobloxAntiAfk.App.ViewModels;

public sealed partial class HotkeyEditorViewModel(
    AntiAfkEngine engine,
    Func<string> otherHotkeyLabel,
    Func<AppSettings, Hotkey?> read,
    Func<AppSettings, Hotkey?, AppSettings> write) : ObservableObject
{
    private const string IdleButtonText = "Поставить своё";

    private string? _problem;

    [ObservableProperty] private string _buttonText = IdleButtonText;
    [ObservableProperty] private bool _isListening;
    [ObservableProperty] private bool _canReset;
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private bool _hasMessage;

    public void Refresh()
    {
        var custom = read(engine.Snapshot.Settings);
        CanReset = custom is not null;
        Message = _problem ?? (custom is { HasModifiers: false }
            ? $"Пока приложение открыто, клавиша {custom.ToLabel(OperatingSystem.IsMacOS())} не будет работать в игре и других программах."
            : "");
        HasMessage = Message.Length > 0;
    }

    [RelayCommand]
    private void StartListening()
    {
        if (IsListening)
        {
            CancelListening();
            return;
        }

        _problem = null;
        IsListening = true;
        ButtonText = "Нажми клавишу или сочетание... Esc: отмена";
        // Иначе текущее сочетание сработает само вместо того, чтобы записаться.
        engine.PauseHotkeys(true);
        Refresh();
    }

    public void CancelListening()
    {
        if (IsListening)
        {
            StopListening();
        }
    }

    public void Capture(Hotkey hotkey)
    {
        if (!IsListening)
        {
            return;
        }

        if (hotkey is { Key: Hotkey.EscapeKey, HasModifiers: false })
        {
            StopListening();
            return;
        }

        var label = hotkey.ToLabel(OperatingSystem.IsMacOS());
        if (label == otherHotkeyLabel())
        {
            _problem = $"{label} уже стоит на другое действие. Выбери другое сочетание.";
            StopListening();
            return;
        }

        engine.UpdateSettings(settings => write(settings, hotkey));
        StopListening();
    }

    [RelayCommand]
    private void Reset()
    {
        _problem = null;
        engine.UpdateSettings(settings => write(settings, null));
    }

    private void StopListening()
    {
        IsListening = false;
        ButtonText = IdleButtonText;
        engine.PauseHotkeys(false);
        Refresh();
    }
}
