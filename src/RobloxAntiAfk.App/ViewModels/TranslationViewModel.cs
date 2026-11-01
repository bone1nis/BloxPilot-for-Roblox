using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobloxAntiAfk.App.Presentation;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Localization;
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
    [ObservableProperty] private bool _selectEachTime;
    [ObservableProperty] private bool _showAreaButton;
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
        HotkeyEditor = new HotkeyEditorViewModel(
            engine,
            () => engine.Snapshot.HotkeyLabel,
            settings => settings.TranslationHotkey,
            (settings, hotkey) => settings with { TranslationHotkey = hotkey });
        translator.Changed += Refresh;
    }

    public HotkeyEditorViewModel HotkeyEditor { get; }

    public void Refresh()
    {
        _isApplying = true;
        var settings = _engine.Snapshot.Settings;
        var snapshot = _translator.Snapshot;
        _isSelecting = snapshot.IsSelecting;

        IsEnabled = settings.TranslationEnabled;
        AutoRefresh = settings.TranslationAutoRefresh;
        SelectEachTime = settings.TranslationSelectEachTime;
        DeepLKey = settings.DeepLKey;

        StatusText = Texts.TranslationStatusText(snapshot);
        IsStatusWarning = Texts.IsTranslationProblem(snapshot.Status);
        Detail = snapshot.Detail;
        HasDetail = snapshot.Detail.Length > 0;
        HotkeyHint = !snapshot.IsHotkeyActive && settings.TranslationEnabled
            ? L.F("Сочетание {0} занять не удалось: его держит другая программа или система. Поставь другое или запускай перевод кнопкой выше.", snapshot.HotkeyLabel)
            : settings.TranslationSelectEachTime
                ? L.F("{0} из любого окна: первое нажатие предложит обвести текст и покажет перевод, следующее его спрячет. Спрятать можно и кликом в окне Roblox.", snapshot.HotkeyLabel)
                : L.F("{0} показывает и прячет перевод из любого окна. Спрятать можно и кликом в окне Roblox. Первое нажатие предложит выбрать область.", snapshot.HotkeyLabel);
        HotkeyEditor.Refresh();

        ShowAreaButton = snapshot.IsSelecting || !settings.TranslationSelectEachTime;
        AreaButtonText = snapshot.IsSelecting ? L.T("Отменить выбор") : settings.TranslationArea is null ? L.T("Выбрать область") : L.T("Выбрать другую область");
        AreaHint = AreaHintText(settings, snapshot.IsSelecting);
        TranslateButtonText = snapshot.IsOverlayActive
            ? L.T("Спрятать перевод")
            : settings.TranslationSelectEachTime ? L.T("Выделить и перевести") : L.T("Перевести сейчас");
        CanTranslate = settings.TranslationEnabled && !snapshot.IsSelecting
            && (settings.TranslationArea is not null || settings.TranslationSelectEachTime);
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

    partial void OnSelectEachTimeChanged(bool value) => Change(settings => settings with { TranslationSelectEachTime = value });

    partial void OnDeepLKeyChanged(string value) => Change(settings => settings with { DeepLKey = value });

    private static string AreaHintText(AppSettings settings, bool isSelecting)
    {
        if (isSelecting)
        {
            return L.T("Зажми левую кнопку мыши в окне Roblox и обведи текст. Этот клик в игру не попадёт. Отмена: Esc или правая кнопка мыши. Через 30 секунд выбор отменится сам.");
        }

        if (settings.TranslationSelectEachTime)
        {
            return L.T("Каждый раз обводишь нужный текст заново: сочетание, выделение, перевод. Клик левой или правой кнопкой в окне Roblox или сочетание прячет перевод.");
        }

        return settings.TranslationArea is null
            ? L.T("Обведи место, где в игре появляется текст: окно диалога, задание или подсказку.")
            : L.T("Область запомнена и двигается вместе с окном Roblox.");
    }

    private void Change(Func<AppSettings, AppSettings> change)
    {
        if (!_isApplying)
        {
            _engine.UpdateSettings(change);
        }
    }
}
