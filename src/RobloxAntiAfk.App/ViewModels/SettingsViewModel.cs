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
    private EngineState _state;
    private bool _isApplying;

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

    public SettingsViewModel(AntiAfkEngine engine)
    {
        _engine = engine;
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
        Targets =
        [
            new ChoiceViewModel<bool>(false, "По центру окна" + DefaultMark, _ => Change(settings => settings with { ClickPoint = null })),
            new ChoiceViewModel<bool>(true, "Своя точка", _ => ChooseOwnPoint()),
        ];
    }

    public IReadOnlyList<ChoiceViewModel<AfkAction>> Actions { get; }
    public IReadOnlyList<ChoiceViewModel<GameKey>> Keys { get; }
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
    private void ResetToDefaults() => _engine.ResetSettings();

    partial void OnIntervalMinutesChanged(double value) => Change(settings => settings with { IntervalMinutes = (int)Math.Round(value) });

    partial void OnAllWindowsChanged(bool value) => Change(settings => settings with { AllWindows = value });

    partial void OnSkipWhilePlayingChanged(bool value) => Change(settings => settings with { SkipWhilePlaying = value });

    partial void OnKeepAwakeChanged(bool value) => Change(settings => settings with { KeepAwake = value });

    private void Change(Func<AppSettings, AppSettings> change)
    {
        if (!_isApplying)
        {
            _engine.UpdateSettings(change);
        }
    }

    private void ChooseOwnPoint()
    {
        if (_engine.Snapshot.Settings.ClickPoint is null && _state != EngineState.PickingPoint)
        {
            _engine.BeginPointPick();
        }
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
