using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobloxAntiAfk.App.Presentation;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;

namespace RobloxAntiAfk.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private const string DefaultMark = " · по умолчанию";
    private const int TightIntervalMinutes = 16;

    private static readonly GameKey[] SelectableKeys =
    [
        GameKey.Space, GameKey.W, GameKey.A, GameKey.S, GameKey.D, GameKey.E,
        GameKey.F, GameKey.Q, GameKey.R, GameKey.One, GameKey.Two, GameKey.Three,
    ];

    private readonly AntiAfkEngine _engine;
    private readonly TelegramNotifier _telegram;
    private EngineState _state;
    private bool _isApplying;
    private string? _telegramResult;

    [ObservableProperty] private double _intervalMinutes = 5;
    [ObservableProperty] private string _intervalText = "";
    [ObservableProperty] private string _intervalHint = "";
    [ObservableProperty] private bool _isIntervalTight;
    [ObservableProperty] private string _actionHint = "";
    [ObservableProperty] private bool _isActionRisky;
    [ObservableProperty] private bool _showTarget;
    [ObservableProperty] private bool _showKeys;
    [ObservableProperty] private string _targetHint = "";
    [ObservableProperty] private bool _showPointButton;
    [ObservableProperty] private bool _canUsePointButton;
    [ObservableProperty] private string _pointButtonText = "";
    [ObservableProperty] private bool _allWindows = true;
    [ObservableProperty] private bool _skipWhilePlaying = true;
    [ObservableProperty] private bool _keepAwake = true;
    [ObservableProperty] private bool _showMacro;
    [ObservableProperty] private string _macroButtonText = "";
    [ObservableProperty] private bool _canUseMacroButton;
    [ObservableProperty] private string _macroHint = "";
    [ObservableProperty] private bool _hideBetweenActions;
    [ObservableProperty] private bool _muteRoblox;
    [ObservableProperty] private bool _showMute;
    [ObservableProperty] private bool _closeToTray;
    [ObservableProperty] private bool _startWithSystem;
    [ObservableProperty] private bool _runOnOpen;
    [ObservableProperty] private string _hotkeyHint = "";
    [ObservableProperty] private bool _reconnectEnabled;
    [ObservableProperty] private string _gameLink = "";
    [ObservableProperty] private string _reconnectHint = "";
    [ObservableProperty] private bool _isReconnectLinkBad;
    [ObservableProperty] private string _telegramToken = "";
    [ObservableProperty] private string _telegramHint = "";
    [ObservableProperty] private bool _canConnectTelegram = true;

    public HotkeyEditorViewModel HotkeyEditor { get; }

    public SettingsViewModel(AntiAfkEngine engine, TelegramNotifier telegram, Func<string> translationHotkeyLabel)
    {
        _engine = engine;
        _telegram = telegram;
        HotkeyEditor = new HotkeyEditorViewModel(
            engine,
            translationHotkeyLabel,
            settings => settings.ToggleHotkey,
            (settings, hotkey) => settings with { ToggleHotkey = hotkey });
        Actions = Enum.GetValues<AfkAction>()
            .Select(action => new ChoiceViewModel<AfkAction>(
                action,
                Texts.ActionName(action) + (action == AfkAction.RightClick ? DefaultMark : ""),
                value => Change(settings => settings with { Action = value })))
            .ToArray();
        Keys = SelectableKeys
            .Select(key => new ChoiceViewModel<GameKey>(
                key,
                Texts.KeyName(key) + (key == GameKey.Space ? DefaultMark : ""),
                value => Change(settings => settings with { Key = value })))
            .ToArray();
        ResetChoices = AppSettings.ResetChoices
            .Select(minutes => new ChoiceViewModel<int>(
                minutes,
                minutes == 0 ? "Не сбрасывать" + DefaultMark : $"Раз в {Texts.Minutes(minutes)}",
                value => Change(settings => settings with { ResetMinutes = value })))
            .ToArray();
        Targets =
        [
            new ChoiceViewModel<bool>(false, "По центру окна" + DefaultMark, _ => Change(settings => settings with { ClickPoint = null })),
            new ChoiceViewModel<bool>(true, "Своя точка", _ => ChooseOwnPoint()),
        ];
    }

    public IReadOnlyList<ChoiceViewModel<AfkAction>> Actions { get; }
    public IReadOnlyList<ChoiceViewModel<GameKey>> Keys { get; }
    public IReadOnlyList<ChoiceViewModel<int>> ResetChoices { get; }
    public IReadOnlyList<ChoiceViewModel<bool>> Targets { get; }
    public string KeyHint => "Раскладка не важна: W нажмётся и на русской.";

    public void Apply(EngineSnapshot snapshot)
    {
        _isApplying = true;
        _state = snapshot.State;
        var settings = snapshot.Settings;

        IntervalMinutes = settings.IntervalMinutes;
        IntervalText = $"раз в {Texts.Minutes(settings.IntervalMinutes)}";
        IsIntervalTight = settings.IntervalMinutes >= TightIntervalMinutes;
        IntervalHint = IsIntervalTight
            ? "Запас маленький: Roblox обычно кикает примерно через 20 минут."
            : "По умолчанию раз в 5 минут. Roblox обычно кикает примерно через 20 минут.";

        foreach (var choice in Actions)
        {
            choice.IsSelected = choice.Value == settings.Action;
        }

        foreach (var choice in Keys)
        {
            choice.IsSelected = choice.Value == settings.Key;
        }

        foreach (var choice in Targets)
        {
            choice.IsSelected = choice.Value == settings.ClickPoint is not null;
        }

        foreach (var choice in ResetChoices)
        {
            choice.IsSelected = choice.Value == settings.ResetMinutes;
        }

        var effect = Texts.Effect(settings.Action);
        ActionHint = effect.Text;
        IsActionRisky = effect.IsWarning;
        ShowTarget = settings.UsesMouse;
        ShowKeys = settings.Action == AfkAction.Key;

        var isPicking = _state == EngineState.PickingPoint;
        ShowPointButton = settings.ClickPoint is not null || isPicking;
        PointButtonText = isPicking ? "Отменить выбор" : "Выбрать другую точку";
        CanUsePointButton = _state is EngineState.PickingPoint or EngineState.Running or EngineState.Stopped;
        TargetHint = TargetHintText(settings, isPicking);

        AllWindows = settings.AllWindows;
        SkipWhilePlaying = settings.SkipWhilePlaying;
        KeepAwake = settings.KeepAwake;

        var isRecording = _state == EngineState.RecordingMacro;
        ShowMacro = settings.Action == AfkAction.Macro;
        MacroButtonText = isRecording ? "Остановить запись" : settings.Macro is null ? "Записать макрос" : "Записать заново";
        CanUseMacroButton = _state is EngineState.RecordingMacro or EngineState.Running or EngineState.Stopped;
        MacroHint = MacroHintText(snapshot, isRecording);

        HideBetweenActions = settings.HideBetweenActions;
        MuteRoblox = settings.MuteRoblox;
        ShowMute = snapshot.CanMuteRoblox;
        CloseToTray = settings.CloseToTray;
        StartWithSystem = settings.StartWithSystem;
        RunOnOpen = settings.RunOnOpen;
        HotkeyHint = snapshot.IsHotkeyActive
            ? $"{snapshot.HotkeyLabel} запускает и останавливает приложение из любого окна, а во время записи останавливает запись."
            : $"Сочетание {snapshot.HotkeyLabel} занять не удалось: его держит другая программа или система. Поставь другое.";
        HotkeyEditor.Refresh();

        ReconnectEnabled = settings.ReconnectEnabled;
        GameLink = settings.GameLink;
        var linkIsValid = Core.Domain.GameLink.ToLaunchUri(settings.GameLink) is not null;
        IsReconnectLinkBad = settings.ReconnectEnabled && !linkIsValid;
        ReconnectHint = ReconnectHintText(settings, linkIsValid);

        TelegramToken = settings.TelegramToken;
        TelegramHint = _telegramResult ?? (settings.TelegramChatId.Length > 0
            ? "Подключено. Об отключении бот напишет, пока приложение открыто, о закрытии окна пока оно запущено. Без интернета сообщение уйдёт, когда он появится."
            : "Создай бота через @BotFather, вставь его токен, напиши боту любое сообщение и нажми «Подключить». Токен хранится на этом компьютере в файле настроек.");
        _isApplying = false;
    }

    [RelayCommand]
    private void PointButton()
    {
        if (_state == EngineState.PickingPoint)
        {
            _engine.CancelPointPick();
        }
        else
        {
            _engine.BeginPointPick();
        }
    }

    [RelayCommand]
    private void MacroButton()
    {
        if (_state == EngineState.RecordingMacro)
        {
            _engine.StopMacroRecording();
        }
        else
        {
            _engine.BeginMacroRecording();
        }
    }

    [RelayCommand]
    private async Task ConnectTelegram()
    {
        var token = _engine.Snapshot.Settings.TelegramToken;
        if (!TelegramNotifier.IsValidToken(token))
        {
            ShowTelegramResult("Это не похоже на токен. Скопируй его из сообщения @BotFather целиком.");
            return;
        }

        CanConnectTelegram = false;
        ShowTelegramResult("Проверяю...");
        var lookup = await _telegram.FindChatAsync(token);
        if (lookup.ChatId is not { } chatId)
        {
            ShowTelegramResult(TelegramProblemText(lookup));
        }
        else
        {
            _engine.UpdateSettings(settings => settings with { TelegramChatId = chatId });
            var isSent = await _telegram.SendAsync(token, chatId, "Roblox AntiAFK подключён. Сюда придёт сообщение, если Roblox отключится или закроется.");
            ShowTelegramResult(isSent ? "Готово: проверочное сообщение отправлено." : "Чат найден, но сообщение не отправилось. Проверь интернет и нажми ещё раз.");
        }

        CanConnectTelegram = true;
    }

    [RelayCommand]
    private void ResetToDefaults() => _engine.ResetSettings();

    partial void OnIntervalMinutesChanged(double value) => Change(settings => settings with { IntervalMinutes = (int)Math.Round(value) });

    partial void OnAllWindowsChanged(bool value) => Change(settings => settings with { AllWindows = value });

    partial void OnSkipWhilePlayingChanged(bool value) => Change(settings => settings with { SkipWhilePlaying = value });

    partial void OnKeepAwakeChanged(bool value) => Change(settings => settings with { KeepAwake = value });

    partial void OnHideBetweenActionsChanged(bool value) => Change(settings => settings with { HideBetweenActions = value });

    partial void OnMuteRobloxChanged(bool value) => Change(settings => settings with { MuteRoblox = value });

    partial void OnCloseToTrayChanged(bool value) => Change(settings => settings with { CloseToTray = value });

    partial void OnStartWithSystemChanged(bool value) => Change(settings => settings with { StartWithSystem = value });

    partial void OnRunOnOpenChanged(bool value) => Change(settings => settings with { RunOnOpen = value });

    partial void OnReconnectEnabledChanged(bool value) => Change(settings => settings with { ReconnectEnabled = value });

    partial void OnGameLinkChanged(string value) => Change(settings => settings with { GameLink = value });

    /// <summary>Чат относится к конкретному боту, поэтому при смене токена его нужно найти заново.</summary>
    partial void OnTelegramTokenChanged(string value)
    {
        if (!_isApplying)
        {
            _telegramResult = null;
            Change(settings => settings with { TelegramToken = value, TelegramChatId = "" });
        }
    }

    private void Change(Func<AppSettings, AppSettings> change)
    {
        if (!_isApplying)
        {
            _engine.UpdateSettings(change);
        }
    }

    private static string TelegramProblemText(TelegramLookup lookup) => lookup.Status switch
    {
        TelegramLookupStatus.TokenRejected => "Telegram не принял этот токен. Скопируй его из сообщения @BotFather заново и целиком.",
        TelegramLookupStatus.Conflict => "Сообщения этого бота уже забирает другая программа или сервис. Создай для уведомлений отдельного бота.",
        TelegramLookupStatus.NoMessages => $"Токен подходит, но новых сообщений нет. Открой {(lookup.BotName is { } name ? $"@{name}" : "своего бота")}, напиши любое сообщение и сразу нажми ещё раз: Telegram хранит их только сутки.",
        _ => "Приложение не смогло связаться с Telegram (api.telegram.org). Интернет может работать, а этот адрес бывает закрыт у провайдера. Если Telegram у тебя работает через VPN, включи VPN для всего компьютера и нажми ещё раз.",
    };

    private void ShowTelegramResult(string text)
    {
        _telegramResult = text;
        TelegramHint = text;
    }

    private void ChooseOwnPoint()
    {
        if (_engine.Snapshot.Settings.ClickPoint is null && _state != EngineState.PickingPoint)
        {
            _engine.BeginPointPick();
        }
    }

    private static string MacroHintText(EngineSnapshot snapshot, bool isRecording)
    {
        if (isRecording)
        {
            return $"Нажимай и кликай в окне Roblox, до минуты. Закончишь: нажми {snapshot.HotkeyLabel} или кнопку выше. Ввод в других окнах не записывается.";
        }

        return snapshot.Settings.Macro is { } macro
            ? $"Записано шагов: {macro.Steps.Count}, длительность {Texts.Duration(macro.Duration)}. Проверить можно кнопкой «Сделать сейчас»."
            : "Запись длится до минуты. Записываются только клавиши и клики в окне Roblox.";
    }

    private static string ReconnectHintText(AppSettings settings, bool linkIsValid)
    {
        if (settings.GameLink.Length > 0 && !linkIsValid)
        {
            return "Нужна ссылка вида roblox.com/games/123456/... или номер игры. Короткие ссылки roblox.com/share не подойдут.";
        }

        return "Зайду сама, если в журнале Roblox появится запись об отключении: максимум 5 попыток подряд. Если закрыть окно самой, обратно не зайду. Подходит для одного окна Roblox.";
    }

    private static string TargetHintText(AppSettings settings, bool isPicking)
    {
        if (isPicking)
        {
            return "Кликни один раз в окне Roblox, этот клик в игру не попадёт. Через 30 секунд выбор отменится сам.";
        }

        return settings.ClickPoint is { } point
            ? $"Точка: {point.X * 100:0}% слева и {point.Y * 100:0}% сверху окна Roblox. Если изменить размер окна, точка сдвинется вместе с ним, а кнопка в игре может оказаться в другом месте."
            : "Нажми «Своя точка» и кликни один раз в окне Roblox. Приложение запомнит это место.";
    }
}
