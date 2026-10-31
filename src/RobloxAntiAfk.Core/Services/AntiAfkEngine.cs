using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Storage;

namespace RobloxAntiAfk.Core.Services;

/// <summary>
/// Главная логика приложения: расписание действий, запуск и остановка, настройки и выбор точки.
/// Все методы вызываются из одного потока интерфейса, поэтому блокировки не нужны.
/// </summary>
public sealed class AntiAfkEngine
{
    private const int MaxActivityEntries = 20;
    private static readonly TimeSpan PickTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CaptureStopFallback = TimeSpan.FromSeconds(1);

    private readonly PlatformServices _platform;
    private readonly IClock _clock;
    private readonly IDataStore<AppSettings> _settingsStore;
    private readonly StatisticsTracker _statistics;
    private readonly ActionRunner _runner;
    private readonly List<ActivityEntry> _activity = [];

    private AppSettings _settings;
    private DateTimeOffset _nextAction;
    private EngineState _state = EngineState.Stopped;
    private bool _isStopped = true;
    private bool _isActing;
    private bool _isPicking;
    private DateTimeOffset _pickDeadline;
    private IReadOnlyList<WindowHandle> _pickWindows = [];
    private WindowHandle _beforePick;
    private int _windowCount;
    private int _actionsThisRun;

    public AntiAfkEngine(
        PlatformServices platform,
        IClock clock,
        IDataStore<AppSettings> settingsStore,
        IDataStore<AfkStatistics> statisticsStore)
    {
        _platform = platform;
        _clock = clock;
        _settingsStore = settingsStore;
        _settings = settingsStore.Load().Normalized();
        _nextAction = clock.Now + _settings.Interval;
        _statistics = new StatisticsTracker(statisticsStore, clock.Now);
        _runner = new ActionRunner(platform, clock);
    }

    public event Action? Changed;

    /// <summary>Выбор точки в выключенном приложении работой не считается.</summary>
    private bool IsWorking => !_isStopped && _state is EngineState.Running or EngineState.Acting or EngineState.PickingPoint;

    public EngineSnapshot Snapshot
    {
        get
        {
            var left = _nextAction - _clock.Now;
            return new EngineSnapshot(
                _state,
                left < TimeSpan.Zero ? TimeSpan.Zero : left,
                _actionsThisRun,
                _statistics.ActiveTimeThisRun,
                _windowCount,
                _settings,
                _statistics.Current,
                _statistics.CurrentSession,
                _statistics.OngoingSession,
                _activity.ToArray());
        }
    }

    /// <summary>Приложение открывается выключенным: ничего не нажимает, пока пользователь сам не запустит.</summary>
    public void Start() => ApplySleepMode();

    /// <summary>Вызывается раз в секунду.</summary>
    public void Tick()
    {
        var now = _clock.Now;
        if (_isPicking && now >= _pickDeadline)
        {
            CancelPointPick();
        }

        var windows = _platform.Windows.FindRobloxWindows();
        if (!_settings.AllWindows && windows.Count > 1)
        {
            windows = [windows[0]];
        }

        _windowCount = windows.Count;
        _state = DetermineState(windows);
        if (_state is EngineState.WaitingForRoblox or EngineState.PermissionRequired)
        {
            _nextAction = now + _settings.Interval;
        }

        _statistics.Update(IsWorking, now);

        if (_state == EngineState.Running && now >= _nextAction)
        {
            _ = RunActionAsync(windows);
        }

        Changed?.Invoke();
    }

    public void ToggleRunning()
    {
        // Учитываем время до переключения, иначе последняя секунда попадёт не в то состояние.
        _statistics.Update(IsWorking, _clock.Now);
        _isStopped = !_isStopped;
        _nextAction = _clock.Now + _settings.Interval;
        if (!_isStopped && !_platform.Permission.IsGranted)
        {
            _platform.Permission.Prompt();
        }

        ApplySleepMode();
        Tick();
    }

    public void RunNow()
    {
        _nextAction = _clock.Now;
        Tick();
    }

    public void UpdateSettings(Func<AppSettings, AppSettings> change)
    {
        var updated = change(_settings).Normalized();
        if (updated.IntervalMinutes != _settings.IntervalMinutes)
        {
            _nextAction = _clock.Now + updated.Interval;
        }

        _settings = updated;
        _settingsStore.Save(_settings);
        ApplySleepMode();
        Changed?.Invoke();
    }

    public void ResetSettings() => UpdateSettings(_ => new AppSettings());

    public void ResetStatistics()
    {
        _statistics.Reset();
        Changed?.Invoke();
    }

    public void OpenPermissionSettings() => _platform.Permission.OpenSystemSettings();

    /// <summary>Ждём один клик в окне Roblox. Клики мимо него проходят как обычно, поэтому мышь не блокируется.</summary>
    public void BeginPointPick()
    {
        if (_isPicking || _isActing)
        {
            return;
        }

        if (!_platform.Permission.IsGranted)
        {
            _platform.Permission.Prompt();
            AddActivity(ActivitySeverity.Error, ActivityCode.PermissionMissing);
            Changed?.Invoke();
            return;
        }

        var windows = _platform.Windows.FindRobloxWindows();
        if (windows.Count == 0)
        {
            AddActivity(ActivitySeverity.Error, ActivityCode.RobloxNotRunning);
            Changed?.Invoke();
            return;
        }

        _beforePick = _platform.Windows.GetForegroundWindow();
        if (!_platform.MouseCapture.Start(OnCapturedMouseDown))
        {
            AddActivity(ActivitySeverity.Error, ActivityCode.PointPickFailed);
            Changed?.Invoke();
            return;
        }

        _isPicking = true;
        _pickWindows = windows;
        _pickDeadline = _clock.Now + PickTimeout;
        if (_platform.Windows.IsMinimized(windows[0]))
        {
            _platform.Windows.Restore(windows[0]);
        }

        _ = _runner.BringToFrontAsync(windows[0]);
        Tick();
    }

    public void CancelPointPick()
    {
        if (!_isPicking)
        {
            return;
        }

        _isPicking = false;
        _platform.MouseCapture.Stop();
        AddActivity(ActivitySeverity.Skipped, ActivityCode.PointPickCancelled);
        _nextAction = _clock.Now + _settings.Interval;
        Changed?.Invoke();
    }

    public void Shutdown()
    {
        _platform.MouseCapture.Stop();
        _platform.Sleep.Apply(SleepMode.Allow);
        _statistics.Finish();
    }

    /// <summary>Вызывается из системного перехвата мыши, поэтому не должен задерживаться.</summary>
    private bool OnCapturedMouseDown(ScreenPoint point)
    {
        if (!_isPicking)
        {
            return false;
        }

        var foreground = _platform.Windows.GetForegroundWindow();
        if (!_pickWindows.Contains(foreground) || _platform.Windows.GetBounds(foreground) is not { } bounds || !bounds.Contains(point))
        {
            return false;
        }

        _isPicking = false;
        _ = FinishPointPickAsync(WindowPoint.FromScreen(point, bounds));
        return true;
    }

    private async Task FinishPointPickAsync(WindowPoint picked)
    {
        await _clock.Yield();
        _nextAction = _clock.Now + _settings.Interval;
        AddActivity(ActivitySeverity.Success, ActivityCode.PointSaved);
        UpdateSettings(settings => settings with { ClickPoint = picked });

        if (_beforePick != WindowHandle.None)
        {
            await _runner.BringToFrontAsync(_beforePick);
        }

        // Обычно перехват снимается при отпускании кнопки. Это запасной выход, если оно до нас не дойдёт.
        await _clock.Delay(CaptureStopFallback);
        if (!_isPicking)
        {
            _platform.MouseCapture.Stop();
        }
    }

    private EngineState DetermineState(IReadOnlyList<WindowHandle> windows)
    {
        if (_isActing)
        {
            return EngineState.Acting;
        }

        if (_isPicking)
        {
            return EngineState.PickingPoint;
        }

        if (_isStopped)
        {
            return EngineState.Stopped;
        }

        if (!_platform.Permission.IsGranted)
        {
            return EngineState.PermissionRequired;
        }

        return windows.Count == 0 ? EngineState.WaitingForRoblox : EngineState.Running;
    }

    private async Task RunActionAsync(IReadOnlyList<WindowHandle> windows)
    {
        _isActing = true;
        _state = EngineState.Acting;
        var settings = _settings;
        Changed?.Invoke();

        try
        {
            var outcome = await _runner.RunAsync(windows, settings);
            RecordOutcome(outcome, settings);
        }
        catch (Exception)
        {
            // Сбой одного действия не должен останавливать приложение: следующее будет по расписанию.
            AddActivity(ActivitySeverity.Error, ActivityCode.UnexpectedError);
        }

        _isActing = false;
        _nextAction = _clock.Now + _settings.Interval;
        Tick();
    }

    private void RecordOutcome(ActionOutcome outcome, AppSettings settings)
    {
        if (outcome.WindowsDone > 0)
        {
            _actionsThisRun += outcome.WindowsDone;
            _statistics.RecordActions(outcome.WindowsDone);
            _activity.Insert(0, new ActivityEntry(
                _clock.Now,
                ActivitySeverity.Success,
                ActivityCode.ActionDone,
                settings.Action,
                settings.Key,
                outcome.WindowsDone,
                outcome.WindowsTotal));
            TrimActivity();
        }
        else if (outcome.WasSkipped)
        {
            AddActivity(ActivitySeverity.Skipped, ActivityCode.SkippedWhilePlaying);
        }
        else
        {
            var code = outcome.Failure == WindowFailure.SwitchFailed ? ActivityCode.SwitchFailed : ActivityCode.WindowUnavailable;
            AddActivity(ActivitySeverity.Error, code);
        }
    }

    private void AddActivity(ActivitySeverity severity, ActivityCode code)
    {
        _activity.Insert(0, new ActivityEntry(_clock.Now, severity, code));
        TrimActivity();
    }

    private void TrimActivity()
    {
        if (_activity.Count > MaxActivityEntries)
        {
            _activity.RemoveRange(MaxActivityEntries, _activity.Count - MaxActivityEntries);
        }
    }

    /// <summary>Во сне Roblox отключится и без всякого кика, поэтому по умолчанию не даём компьютеру уснуть.</summary>
    private void ApplySleepMode() => _platform.Sleep.Apply(
        _isStopped ? SleepMode.Allow : _settings.KeepAwake ? SleepMode.KeepAwake : SleepMode.AllowButStayResponsive);
}
