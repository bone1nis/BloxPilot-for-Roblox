using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobloxAntiAfk.App.Presentation;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;

namespace RobloxAntiAfk.App.ViewModels;

public sealed record ActivityItem(string Time, string Text, ActivitySeverity Severity)
{
    public bool IsSuccess => Severity == ActivitySeverity.Success;
    public bool IsSkipped => Severity == ActivitySeverity.Skipped;
    public bool IsError => Severity == ActivitySeverity.Error;
}

public sealed partial class StatusViewModel(AntiAfkEngine engine) : ObservableObject
{
    private IReadOnlyList<ActivityEntry> _shownActivity = [];

    [ObservableProperty] private string _title = "Запускаюсь";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private string _timeText = "";
    [ObservableProperty] private bool _showCountdown;
    [ObservableProperty] private double _ringSweep = 360;
    [ObservableProperty] private string _runButtonText = "Запустить";
    [ObservableProperty] private bool _canToggleRunning;
    [ObservableProperty] private bool _canRunNow;
    [ObservableProperty] private bool _needsPermission;
    [ObservableProperty] private string _actionsDone = "0";
    [ObservableProperty] private string _activeTime = "0:00";
    [ObservableProperty] private bool _isStopped;
    [ObservableProperty] private bool _isWaiting;
    [ObservableProperty] private bool _isProblem;
    [ObservableProperty] private bool _hasActivity;

    public ObservableCollection<ActivityItem> Activity { get; } = [];

    public void Apply(EngineSnapshot snapshot)
    {
        var settings = snapshot.Settings;
        var state = snapshot.State;

        (Title, Subtitle, var word) = state switch
        {
            EngineState.Acting => ("Выполняю", "Сейчас вернусь обратно", "Сейчас"),
            EngineState.PickingPoint => ("Выбери точку", "Кликни один раз в окне Roblox", "Точка"),
            EngineState.RecordingMacro => ("Идёт запись", RecordingSubtitle(snapshot), "Запись"),
            EngineState.Stopped => ("Выключено", "Нажми «Запустить», когда будешь готова", "Выкл"),
            EngineState.WaitingForRoblox => ("Жду Roblox", "Запусти игру, и я начну работать", "Жду"),
            EngineState.PermissionRequired => ("Нужно разрешение", "Без него macOS не даст ничего нажать", "Стоп"),
            _ => ("Работает", RunningSubtitle(snapshot), (string?)null),
        };

        ShowCountdown = word is null;
        TimeText = word ?? Texts.Duration(RoundUp(snapshot.TimeUntilNextAction));
        RingSweep = word is null ? 360 * snapshot.TimeUntilNextAction / settings.Interval : 360;

        IsStopped = state == EngineState.Stopped;
        IsWaiting = state is EngineState.WaitingForRoblox or EngineState.PickingPoint or EngineState.RecordingMacro;
        IsProblem = state == EngineState.PermissionRequired;

        RunButtonText = state == EngineState.Stopped ? "Запустить" : "Остановить";
        CanToggleRunning = state is not (EngineState.Acting or EngineState.PickingPoint or EngineState.RecordingMacro);
        CanRunNow = state == EngineState.Running;
        NeedsPermission = state == EngineState.PermissionRequired;

        ActionsDone = snapshot.ActionsThisRun.ToString();
        ActiveTime = Texts.Duration(snapshot.ActiveTimeThisRun);
        ApplyActivity(snapshot.Activity);
    }

    [RelayCommand]
    private void ToggleRunning() => engine.ToggleRunning();

    [RelayCommand]
    private void RunNow() => engine.RunNow();

    [RelayCommand]
    private void OpenPermissionSettings() => engine.OpenPermissionSettings();

    private static string RunningSubtitle(EngineSnapshot snapshot)
    {
        var text = $"{Texts.ActionSummary(snapshot.Settings)} раз в {Texts.Minutes(snapshot.Settings.IntervalMinutes)}";
        return snapshot.WindowCount > 1 ? $"{text} · окон: {snapshot.WindowCount}" : text;
    }

    private static string RecordingSubtitle(EngineSnapshot snapshot) =>
        $"Действуй в окне Roblox, осталось {Texts.Duration(RoundUp(snapshot.RecordingLeft))}";

    private static TimeSpan RoundUp(TimeSpan time) => TimeSpan.FromSeconds(Math.Ceiling(time.TotalSeconds));

    private void ApplyActivity(IReadOnlyList<ActivityEntry> entries)
    {
        if (entries.SequenceEqual(_shownActivity))
        {
            return;
        }

        _shownActivity = entries;
        Activity.Clear();
        foreach (var entry in entries)
        {
            Activity.Add(new ActivityItem(entry.Time.ToLocalTime().ToString("HH:mm"), Texts.Activity(entry), entry.Severity));
        }

        HasActivity = entries.Count > 0;
    }
}
