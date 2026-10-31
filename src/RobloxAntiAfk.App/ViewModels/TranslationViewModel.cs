using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobloxAntiAfk.App.Presentation;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;

namespace RobloxAntiAfk.App.ViewModels;

public sealed partial class TranslationViewModel : ObservableObject
{
    private readonly AntiAfkEngine _engine;
    private readonly ScreenTranslator _translator;
    private bool _isApplying;
    private bool _isSelecting;

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private bool _autoRefresh;
    [ObservableProperty] private string _deepLKey = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isStatusWarning;
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private bool _hasDetail;
    [ObservableProperty] private string _hotkeyHint = "";
    [ObservableProperty] private string _areaButtonText = "";
    [ObservableProperty] private string _areaHint = "";
    [ObservableProperty] private string _translateButtonText = "";
    [ObservableProperty] private bool _canTranslate;
    [ObservableProperty] private bool _showPermission;
    [ObservableProperty] private string _sourceText = "";
    [ObservableProperty] private string _translatedText = "";
    [ObservableProperty] private bool _hasResult;

    public TranslationViewModel(AntiAfkEngine engine, ScreenTranslator translator)
    {
        _engine = engine;
        _translator = translator;
        translator.Changed += Refresh;
    }

    public void Refresh()
    {
        _isApplying = true;
        var settings = _engine.Snapshot.Settings;
        var snapshot = _translator.Snapshot;
        _isSelecting = snapshot.IsSelecting;

        IsEnabled = settings.TranslationEnabled;
        AutoRefresh = settings.TranslationAutoRefresh;
        DeepLKey = settings.DeepLKey;

        StatusText = Texts.TranslationStatusText(snapshot);
        IsStatusWarning = Texts.IsTranslationProblem(snapshot.Status);
        Detail = snapshot.Detail;
        HasDetail = snapshot.Detail.Length > 0;
        HotkeyHint = snapshot.IsHotkeyActive || !settings.TranslationEnabled
            ? $"{snapshot.HotkeyLabel} показывает и прячет перевод из любого окна. Первое нажатие предложит выбрать область."
            : $"Сочетание {snapshot.HotkeyLabel} занято другой программой. Перевод работает только кнопкой ниже.";

        AreaButtonText = snapshot.IsSelecting ? "Отменить выбор" : settings.TranslationArea is null ? "Выбрать область" : "Выбрать другую область";
        AreaHint = snapshot.IsSelecting
            ? "Зажми левую кнопку мыши в окне Roblox и обведи текст. Этот клик в игру не попадёт. Через 30 секунд выбор отменится сам."
            : settings.TranslationArea is null
                ? "Обведи место, где в игре появляется текст: окно диалога, задание или подсказку."
                : "Область запомнена и двигается вместе с окном Roblox.";
        TranslateButtonText = snapshot.IsOverlayActive ? "Спрятать перевод" : "Перевести сейчас";
        CanTranslate = settings.TranslationEnabled && !snapshot.IsSelecting && settings.TranslationArea is not null;
        ShowPermission = settings.TranslationEnabled && !snapshot.IsScreenPermissionGranted;

        SourceText = snapshot.SourceText;
        TranslatedText = snapshot.TranslatedText;
        HasResult = snapshot.SourceText.Length > 0;
        _isApplying = false;
    }

    [RelayCommand]
    private void AreaButton()
    {
        if (_isSelecting)
        {
            _translator.CancelSelection();
        }
        else
        {
            _translator.BeginSelection();
        }
    }

    [RelayCommand]
    private void TranslateButton()
    {
        if (_translator.Snapshot.IsOverlayActive)
        {
            _translator.Close();
        }
        else
        {
            _translator.TranslateNow();
        }
    }

    [RelayCommand]
    private void OpenPermissionSettings() => _translator.OpenPermissionSettings();

    partial void OnIsEnabledChanged(bool value) => Change(settings => settings with { TranslationEnabled = value });

    partial void OnAutoRefreshChanged(bool value) => Change(settings => settings with { TranslationAutoRefresh = value });

    partial void OnDeepLKeyChanged(string value) => Change(settings => settings with { DeepLKey = value });

    private void Change(Func<AppSettings, AppSettings> change)
    {
        if (!_isApplying)
        {
            _engine.UpdateSettings(change);
        }
    }
}
