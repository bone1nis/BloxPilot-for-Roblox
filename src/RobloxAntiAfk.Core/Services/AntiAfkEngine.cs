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
    private const int MaxReconnectAttempts = 5;
    private static readonly TimeSpan PickTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CaptureStopFallback = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan AudioRefresh = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RejoinCheckDelay = TimeSpan.FromSeconds(90);

    private readonly PlatformServices _platform;
    private readonly IClock _clock;
    private readonly IDataStore<AppSettings> _settingsStore;
    private readonly StatisticsTracker _statistics;
    private readonly ActionRunner _runner;
    private readonly List<ActivityEntry> _activity = [];

    private AppSettings _settings;
    private DateTimeOffset _nextAction;
    private DateTimeOffset _nextReset;
    private EngineState _state = EngineState.Stopped;
    private bool _isStopped = true;
    private bool _isActing;
    private bool _isPicking;
    private bool _isHotkeyActive;
    private bool _isHotkeyStarted;
    private Hotkey? _boundHotkey;
    private DateTimeOffset _pickDeadline;
    private IReadOnlyList<WindowHandle> _pickWindows = [];
    private WindowHandle _beforePick;
    private int _windowCount;
    private int _actionsThisRun;
    private CancellationTokenSource? _actionCancel;

    private MacroRecorder? _macroRecorder;
    private DateTimeOffset _recordDeadline;
    private ScreenRect? _recordBounds;

    private bool _isMuted;
    private int _mutedWindowCount;
    private DateTimeOffset _nextAudioRefresh;

    private bool _hadWindows;
    private int _reconnectAttempts;
    private DateTimeOffset? _reconnectAt;
    private DateTimeOffset? _rejoinCheckAt;

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

    /// <summary>Каждая новая запись в журнале, кроме обычных действий по расписанию.</summary>
    public event Action<ActivityEntry>? ActivityAdded;

    private bool IsRecording => _macroRecorder is not null;

    /// <summary>Выбор точки в выключенном приложении работой не считается.</summary>
    private bool IsWorking => !_isStopped && _state is EngineState.Running or EngineState.Acting or EngineState.PickingPoint;

    public EngineSnapshot Snapshot
    {
        get
        {
            var left = _nextAction - _clock.Now;
            var recordingLeft = _recordDeadline - _clock.Now;
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
                _activity.ToArray())
            {
                IsHotkeyActive = _isHotkeyActive,
                HotkeyLabel = _platform.Hotkey.Label,
                CanMuteRoblox = _platform.Audio.IsSupported,
                RecordingLeft = IsRecording && recordingLeft > TimeSpan.Zero ? recordingLeft : TimeSpan.Zero,
            };
        }
    }

    /// <summary>По умолчанию приложение открывается выключенным: ничего не нажимает, пока пользователь сам не запустит.</summary>
    public void Start()
    {
        _platform.Disconnects.Arm();
        ApplySleepMode();
        SyncHotkey();

        // Приложение могли переместить или переименовать при обновлении, а автозапуск хранит полный путь.
        if (_settings.StartWithSystem)
        {
            _platform.AutoStart.Apply(true);
        }

        if (_settings.RunOnOpen)
        {
            ToggleRunning();
        }
    }

    /// <summary>Вызывается раз в секунду.</summary>
    public void Tick()
    {
        var now = _clock.Now;
        if (_isPicking && now >= _pickDeadline)
        {
            CancelPointPick();
        }

        if (_macroRecorder is { } recorder && (now >= _recordDeadline || recorder.IsFull))
        {
            StopMacroRecording();
            return;
        }

        var windows = _platform.Windows.FindRobloxWindows();
        var allWindows = windows;
        if (!_settings.AllWindows && windows.Count > 1)
        {
            windows = [windows[0]];
        }

        RefreshRecordingTarget();
        _windowCount = windows.Count;
        _state = DetermineState(windows);
        if (_state is EngineState.WaitingForRoblox or EngineState.PermissionRequired)
        {
            _nextAction = now + _settings.Interval;
        }

        _statistics.Update(IsWorking, now);
        SyncAudio(allWindows, now);
        WatchConnection(allWindows, now);

        if (_state == EngineState.Running && now >= _nextAction)
        {
            _ = RunActionAsync(windows);
        }

        Changed?.Invoke();
    }

    public void ToggleRunning()
    {
        // Учитываем время до переключения, иначе последняя секунда попадёт не в то состояние.
        var now = _clock.Now;
        _statistics.Update(IsWorking, now);
        _isStopped = !_isStopped;
        _nextAction = now + _settings.Interval;
        _nextReset = now + TimeSpan.FromMinutes(_settings.ResetMinutes);
        _hadWindows = false;
        _reconnectAttempts = 0;
        _reconnectAt = null;
        _rejoinCheckAt = null;

        if (_isStopped)
        {
            _actionCancel?.Cancel();
        }
        else
        {
            _platform.Disconnects.Arm();
            if (!_platform.Permission.IsGranted)
            {
                _platform.Permission.Prompt();
            }
        }

        ApplySleepMode();
        Tick();
    }

    public void RunNow()
    {
        _nextAction = _clock.Now;
        Tick();
    }

    /// <summary>Пока пользователь выделяет область, действие по расписанию помешало бы ему.</summary>
    public void PostponeNextAction(TimeSpan duration)
    {
        var earliest = _clock.Now + duration;
        if (_nextAction < earliest)
        {
            _nextAction = earliest;
        }
    }

    public void UpdateSettings(Func<AppSettings, AppSettings> change)
    {
        var updated = change(_settings).Normalized();
        if (updated.IntervalMinutes != _settings.IntervalMinutes)
        {
            _nextAction = _clock.Now + updated.Interval;
        }

        if (updated.ResetMinutes != _settings.ResetMinutes)
        {
            _nextReset = _clock.Now + TimeSpan.FromMinutes(updated.ResetMinutes);
        }

        if (updated.StartWithSystem != _settings.StartWithSystem)
        {
            _platform.AutoStart.Apply(updated.StartWithSystem);
        }

        _settings = updated;
        _settingsStore.Save(_settings);
        ApplySleepMode();
        SyncHotkey();
        SyncAudio(_platform.Windows.FindRobloxWindows(), _clock.Now, force: true);
        Changed?.Invoke();
    }

    public bool AreHotkeysPaused { get; private set; }

    public bool IsActing => _isActing;

    /// <summary>Пока записывается новое сочетание, старые не должны перехватывать нажатия.</summary>
    public void PauseHotkeys(bool isPaused)
    {
        if (AreHotkeysPaused != isPaused)
        {
            AreHotkeysPaused = isPaused;
            SyncHotkey();
            Changed?.Invoke();
        }
    }

    private void SyncHotkey()
    {
        if (_isHotkeyStarted && (AreHotkeysPaused || _settings.ToggleHotkey != _boundHotkey))
        {
            _platform.Hotkey.Stop();
            _isHotkeyStarted = false;
        }

        if (!_isHotkeyStarted && !AreHotkeysPaused)
        {
            _boundHotkey = _settings.ToggleHotkey;
            _platform.Hotkey.Bind(_boundHotkey);
            _isHotkeyActive = _platform.Hotkey.Start(HandleHotkey);
            _isHotkeyStarted = true;
        }
    }

    public void ResetSettings() => UpdateSettings(settings => settings.ToDefaultsKeepingUserData());

    public void ResetStatistics()
    {
        _statistics.Reset();
        Changed?.Invoke();
    }

    public void OpenPermissionSettings() => _platform.Permission.OpenSystemSettings();

    /// <summary>Ждём один клик в окне Roblox. Клики мимо него проходят как обычно, поэтому мышь не блокируется.</summary>
    public void BeginPointPick()
    {
        if (_isPicking || _isActing || IsRecording || !TryGetWindowsForCapture(out var windows))
        {
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
        BringForCapture(windows[0]);
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

    /// <summary>Записывает ввод в окне Roblox. Всё, что нажато в других окнах, в макрос не попадает.</summary>
    public void BeginMacroRecording()
    {
        if (_isPicking || _isActing || IsRecording || !TryGetWindowsForCapture(out var windows))
        {
            return;
        }

        var now = _clock.Now;
        _beforePick = _platform.Windows.GetForegroundWindow();
        if (!_platform.Recorder.Start(OnRecordedInput))
        {
            AddActivity(ActivitySeverity.Error, ActivityCode.MacroRecordFailed);
            Changed?.Invoke();
            return;
        }

        _macroRecorder = new MacroRecorder(now);
        _recordDeadline = now + MacroRecorder.MaxDuration;
        _pickWindows = windows;
        BringForCapture(windows[0]);
        Tick();
    }

    public void StopMacroRecording()
    {
        if (_macroRecorder is not { } recorder)
        {
            return;
        }

        _macroRecorder = null;
        _recordBounds = null;
        _platform.Recorder.Stop();
        var macro = recorder.Finish();
        _nextAction = _clock.Now + _settings.Interval;
        if (macro.Steps.Count == 0)
        {
            AddActivity(ActivitySeverity.Skipped, ActivityCode.MacroEmpty);
        }
        else
        {
            AddActivity(ActivitySeverity.Success, ActivityCode.MacroSaved);
            UpdateSettings(settings => settings with { Macro = macro });
        }

        if (_beforePick != WindowHandle.None)
        {
            _ = _runner.BringToFrontAsync(_beforePick);
        }

        Tick();
    }

    public void Shutdown()
    {
        _actionCancel?.Cancel();
        _platform.MouseCapture.Stop();
        _platform.Recorder.Stop();
        _platform.Hotkey.Stop();
        _platform.Sleep.Apply(SleepMode.Allow);
        if (_isMuted)
        {
            _platform.Audio.SetMuted(_platform.Windows.FindRobloxWindows(), false);
            _isMuted = false;
        }

        _statistics.Finish();
    }

    /// <summary>Во время записи сочетание останавливает запись: кнопка в приложении может быть за окном игры.</summary>
    private void HandleHotkey()
    {
        // Одиночной клавишей может оказаться та, что приложение само нажимает в игре.
        if (_isActing && _settings.ToggleHotkey is { HasModifiers: false })
        {
            return;
        }

        if (IsRecording)
        {
            StopMacroRecording();
        }
        else if (!_isPicking)
        {
            ToggleRunning();
        }
    }

    private bool TryGetWindowsForCapture(out IReadOnlyList<WindowHandle> windows)
    {
        windows = [];
        if (!_platform.Permission.IsGranted)
        {
            _platform.Permission.Prompt();
            AddActivity(ActivitySeverity.Error, ActivityCode.PermissionMissing);
            Changed?.Invoke();
            return false;
        }

        windows = _platform.Windows.FindRobloxWindows();
        if (windows.Count == 0)
        {
            AddActivity(ActivitySeverity.Error, ActivityCode.RobloxNotRunning);
            Changed?.Invoke();
            return false;
        }

        return true;
    }

    private void BringForCapture(WindowHandle window)
    {
        if (_platform.Windows.IsMinimized(window))
        {
            _platform.Windows.Restore(window);
        }

        _ = _runner.BringToFrontAsync(window);
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

    /// <summary>Вызывается из системного перехвата. Границы окна для движения мыши берём из запомненных: их запрос слишком дорогой.</summary>
    private void OnRecordedInput(RawInputEvent input)
    {
        if (_macroRecorder is not { } recorder)
        {
            return;
        }

        if (input.Kind != MacroStepKind.MouseMove)
        {
            RefreshRecordingTarget();
        }

        if (_recordBounds is not null)
        {
            recorder.Add(input, _recordBounds, _clock.Now);
        }
    }

    /// <summary>Пока Roblox не на переднем плане, границ нет, и ввод в макрос не попадает.</summary>
    private void RefreshRecordingTarget()
    {
        if (!IsRecording)
        {
            return;
        }

        var foreground = _platform.Windows.GetForegroundWindow();
        _recordBounds = _pickWindows.Contains(foreground) ? _platform.Windows.GetBounds(foreground) : null;
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

        if (IsRecording)
        {
            return EngineState.RecordingMacro;
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
        var settings = _settings;
        if (settings.Action == AfkAction.Macro && settings.Macro is not { Steps.Count: > 0 })
        {
            AddActivity(ActivitySeverity.Error, ActivityCode.MacroMissing);
            _nextAction = _clock.Now + _settings.Interval;
            return;
        }

        _isActing = true;
        _state = EngineState.Acting;
        Changed?.Invoke();

        var resetCharacter = settings.ResetMinutes > 0 && _clock.Now >= _nextReset;
        using var cancel = new CancellationTokenSource();
        _actionCancel = cancel;

        try
        {
            var outcome = await _runner.RunAsync(windows, settings, resetCharacter, cancel.Token);
            RecordOutcome(outcome, settings);
            if (resetCharacter && outcome.WindowsDone > 0)
            {
                _nextReset = _clock.Now + TimeSpan.FromMinutes(settings.ResetMinutes);
                AddActivity(ActivitySeverity.Success, ActivityCode.CharacterReset);
            }
        }
        catch (Exception)
        {
            // Сбой одного действия не должен останавливать приложение: следующее будет по расписанию.
            AddActivity(ActivitySeverity.Error, ActivityCode.UnexpectedError);
        }

        _actionCancel = null;
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

    /// <summary>
    /// О закрытии окна только сообщаем: его мог закрыть сам пользователь.
    /// Заходим обратно лишь после записи об отключении в журнале клиента.
    /// </summary>
    private void WatchConnection(IReadOnlyList<WindowHandle> windows, DateTimeOffset now)
    {
        // Об отключении сообщаем и в выключенном приложении: человек может играть сам. Обратно заходим только в запущенном.
        var isDisconnected = _platform.Disconnects.Poll();
        if (isDisconnected)
        {
            AddActivity(ActivitySeverity.Error, ActivityCode.Disconnected);
        }

        if (_isStopped)
        {
            return;
        }

        if (_hadWindows && windows.Count == 0)
        {
            AddActivity(ActivitySeverity.Error, ActivityCode.RobloxClosed);
        }

        _hadWindows = windows.Count > 0;

        if (isDisconnected)
        {
            _rejoinCheckAt = null;
            _reconnectAt = now;
        }

        if (!_settings.ReconnectEnabled || _isActing || GameLink.ToLaunchUri(_settings.GameLink) is not { } launchUri)
        {
            return;
        }

        if (_reconnectAt is { } reconnectAt && now >= reconnectAt)
        {
            _reconnectAt = null;
            if (_reconnectAttempts >= MaxReconnectAttempts)
            {
                AddActivity(ActivitySeverity.Error, ActivityCode.ReconnectGaveUp);
                return;
            }

            _reconnectAttempts++;
            _platform.Launcher.Open(launchUri);
            _rejoinCheckAt = now + RejoinCheckDelay;
            AddActivity(ActivitySeverity.Skipped, ActivityCode.Reconnecting);
        }
        else if (_rejoinCheckAt is { } rejoinCheckAt && now >= rejoinCheckAt)
        {
            _rejoinCheckAt = null;
            if (windows.Count > 0)
            {
                _reconnectAttempts = 0;
                AddActivity(ActivitySeverity.Success, ActivityCode.Reconnected);
            }
            else
            {
                _reconnectAt = now + ReconnectDelay;
            }
        }
    }

    /// <summary>Звук у нового окна появляется не сразу, поэтому отключение время от времени повторяем.</summary>
    private void SyncAudio(IReadOnlyList<WindowHandle> windows, DateTimeOffset now, bool force = false)
    {
        if (!_platform.Audio.IsSupported)
        {
            return;
        }

        var shouldMute = !_isStopped && _settings.MuteRoblox;
        if (!shouldMute && !_isMuted)
        {
            return;
        }

        if (!force && shouldMute == _isMuted && windows.Count == _mutedWindowCount && now < _nextAudioRefresh)
        {
            return;
        }

        _platform.Audio.SetMuted(windows, shouldMute);
        _isMuted = shouldMute;
        _mutedWindowCount = windows.Count;
        _nextAudioRefresh = now + AudioRefresh;
    }

    private void AddActivity(ActivitySeverity severity, ActivityCode code)
    {
        var entry = new ActivityEntry(_clock.Now, severity, code);
        _activity.Insert(0, entry);
        TrimActivity();
        ActivityAdded?.Invoke(entry);
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
