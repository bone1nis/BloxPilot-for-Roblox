using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;

namespace RobloxAntiAfk.Core.Services;

/// <summary>Показывает подпись поверх игры. Реализация живёт в интерфейсе, здесь нужны только эти команды.</summary>
public interface ITranslationOverlay
{
    /// <summary>Закрывает окно Roblox застывшим кадром с подсказкой.</summary>
    void ShowFrozen(ScreenSnapshot snapshot, string prompt);

    /// <summary>Рамка поверх застывшего кадра.</summary>
    void ShowSelection(ScreenRect area);

    /// <summary>Возвращает место, где оказалась подпись.</summary>
    ScreenRect ShowText(ScreenRect area, ScreenRect window, string text, bool isError);

    void Hide();
}

/// <summary>
/// Перевод надписей с экрана: выбор области, снимок, распознавание, перевод и подпись поверх игры.
/// В процесс Roblox ничего не внедряется. Экран читается, только пока Roblox на переднем плане,
/// иначе в сервис перевода мог бы уйти текст из другого окна. Все методы вызываются из потока интерфейса.
/// </summary>
public sealed class ScreenTranslator
{
    public const double MinAreaSize = 12;
    private const string SelectionPrompt = "Зажми левую кнопку мыши и обведи текст";
    private static readonly TimeSpan SelectionTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MessageDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan OverlayHideDelay = TimeSpan.FromMilliseconds(80);

    private readonly PlatformServices _platform;
    private readonly AntiAfkEngine _engine;
    private readonly ITranslationOverlay _overlay;
    private readonly Func<string, string, CancellationToken, Task<string?>> _translate;
    private readonly IClock _clock;

    private TranslationStatus _status;
    private string _source = "";
    private string _translated = "";
    private string _detail = "";
    private bool _isHotkeyActive;
    private bool _isHotkeyRegistered;

    private bool _isSelecting;
    private bool _isDragging;
    private bool _isDragUpdatePending;
    private DateTimeOffset _selectionDeadline;
    private ScreenRect _selectionBounds;
    private ScreenSnapshot? _snapshot;
    private ScreenPoint _dragStart;
    private ScreenPoint _dragCurrent;

    /// <summary>Пользователь включил подпись, но на экране её может не быть: окно Roblox сейчас не впереди.</summary>
    private bool _isActive;
    private bool _isBusy;
    private bool _isOverlayVisible;
    private bool _shownIsError;
    private string _shownText = "";
    private ScreenRect _shownWindow;
    private ScreenRect _shownPanel;
    private DateTimeOffset? _messageUntil;
    private DateTimeOffset _nextRefresh;
    private CancellationTokenSource? _readCancel;

    public ScreenTranslator(
        PlatformServices platform,
        AntiAfkEngine engine,
        ITranslationOverlay overlay,
        Func<string, string, CancellationToken, Task<string?>> translate,
        IClock clock)
    {
        _platform = platform;
        _engine = engine;
        _overlay = overlay;
        _translate = translate;
        _clock = clock;
        _status = IdleStatus(Settings);
    }

    public event Action? Changed;

    private AppSettings Settings => _engine.Snapshot.Settings;

    public TranslationSnapshot Snapshot => new(
        _status,
        _isSelecting,
        _isActive,
        _isHotkeyActive,
        _platform.TranslationHotkey.Label,
        _platform.TextReader.IsPermissionGranted,
        _source,
        _translated,
        _detail);

    /// <summary>Сочетание занимаем, только пока перевод включён: иначе оно незачем другим программам мешать.</summary>
    public void SyncWithSettings()
    {
        var settings = Settings;
        if (settings.TranslationEnabled && !_isHotkeyRegistered)
        {
            _isHotkeyActive = _platform.TranslationHotkey.Start(HandleHotkey);
            _isHotkeyRegistered = true;
        }
        else if (!settings.TranslationEnabled && _isHotkeyRegistered)
        {
            _platform.TranslationHotkey.Stop();
            _isHotkeyRegistered = false;
            _isHotkeyActive = false;
            CancelSelection();
            Close();
        }

        if (!_isSelecting && !_isActive && !_isBusy && IsIdleStatus(_status))
        {
            _status = IdleStatus(settings);
        }
    }

    /// <summary>Вызывается раз в секунду.</summary>
    public void Tick()
    {
        var now = _clock.Now;
        if (_isSelecting && now >= _selectionDeadline)
        {
            CancelSelection();
        }

        if (_messageUntil is { } until && now >= until)
        {
            _messageUntil = null;
            if (!_isActive)
            {
                HideOverlay();
            }
        }

        if (!_isActive || _isBusy)
        {
            return;
        }

        var window = FindForegroundRoblox();
        if (window == WindowHandle.None || _platform.Windows.GetBounds(window) is not { } bounds)
        {
            // Подпись не должна висеть поверх других программ.
            HideOverlay();
            return;
        }

        var area = Settings.TranslationArea?.ToScreen(bounds);
        if (area is { } shownArea && _shownText.Length > 0 && (!_isOverlayVisible || bounds != _shownWindow))
        {
            ShowOnOverlay(shownArea, bounds, _shownText, _shownIsError);
        }

        if (Settings.TranslationAutoRefresh && now >= _nextRefresh)
        {
            _ = ReadAndTranslateAsync(isUpdate: true);
        }
    }

    /// <summary>Сочетание показывает и прячет подпись, поэтому к приложению возвращаться не нужно.</summary>
    public void HandleHotkey()
    {
        if (_isSelecting)
        {
            CancelSelection();
        }
        else if (_isActive)
        {
            Close();
        }
        else if (Settings.TranslationArea is null || Settings.TranslationSelectEachTime)
        {
            BeginSelection();
        }
        else
        {
            TranslateNow();
        }
    }

    public void BeginSelection()
    {
        if (!_isSelecting && !_isBusy && CanUseMouse())
        {
            _ = BeginSelectionAsync();
        }
    }

    public void CancelSelection()
    {
        if (!_isSelecting)
        {
            return;
        }

        _isSelecting = false;
        _isDragging = false;
        _platform.MouseCapture.Stop();
        HideOverlay();
        ReleaseSnapshot();
        SetStatus(IdleStatus(Settings));
    }

    /// <summary>Кнопка в приложении: окно Roblox сначала нужно вывести вперёд, иначе снимок будет не тот.</summary>
    public void TranslateNow()
    {
        if (Settings.TranslationSelectEachTime)
        {
            BeginSelection();
        }
        else if (!_isSelecting && !_isBusy)
        {
            _ = ReadAndTranslateAsync(isUpdate: false);
        }
    }

    public void Close()
    {
        _readCancel?.Cancel();
        _isActive = false;
        _shownText = "";
        _messageUntil = null;
        HideOverlay();
        if (!_isSelecting)
        {
            _status = IdleStatus(Settings);
        }

        Changed?.Invoke();
    }

    public void OpenPermissionSettings() => _platform.TextReader.OpenPermissionSettings();

    public void Shutdown()
    {
        _readCancel?.Cancel();
        CancelSelection();
        if (_isHotkeyRegistered)
        {
            _platform.TranslationHotkey.Stop();
            _isHotkeyRegistered = false;
        }

        HideOverlay();
        ReleaseSnapshot();
        _platform.Overlay.Release();
    }

    /// <summary>
    /// Кадр снимается до перехвата мыши: пока его нет, рисовать рамку не на чем.
    /// Roblox должен быть впереди, иначе в кадр попадёт другая программа.
    /// </summary>
    private async Task BeginSelectionAsync()
    {
        var hadOverlay = _isOverlayVisible;
        Close();
        if (!_platform.Permission.IsGranted)
        {
            _platform.Permission.Prompt();
            Report(TranslationStatus.InputPermissionMissing);
            return;
        }

        if (!_platform.TextReader.IsPermissionGranted)
        {
            _platform.TextReader.RequestPermission();
            Report(TranslationStatus.ScreenPermissionMissing);
            return;
        }

        var windows = _platform.Windows.FindRobloxWindows();
        if (windows.Count == 0)
        {
            Report(TranslationStatus.RobloxMissing);
            return;
        }

        _isSelecting = true;
        _isDragging = false;
        _selectionDeadline = _clock.Now + SelectionTimeout;
        _engine.PostponeNextAction(SelectionTimeout);
        SetStatus(TranslationStatus.SelectingArea);

        try
        {
            var foreground = _platform.Windows.GetForegroundWindow();
            if (!windows.Contains(foreground))
            {
                foreground = windows[0];
                BringForward(foreground);
                await _clock.Delay(_platform.WindowSettleDelay);
            }
            else if (hadOverlay)
            {
                await _clock.Delay(OverlayHideDelay);
            }

            if (_platform.Windows.GetForegroundWindow() != foreground || _platform.Windows.GetBounds(foreground) is not { } bounds)
            {
                AbortSelection(TranslationStatus.RobloxNotInFront);
                return;
            }

            var capture = await _platform.TextReader.CaptureAsync(bounds, CancellationToken.None);
            if (!_isSelecting)
            {
                capture.Snapshot?.Dispose();
                return;
            }

            if (capture.Status != TextReadStatus.Success || capture.Snapshot is null)
            {
                capture.Snapshot?.Dispose();
                _detail = capture.Detail;
                AbortSelection(ToStatus(capture.Status));
                return;
            }

            _snapshot = capture.Snapshot;
            _selectionBounds = bounds;
            _overlay.ShowFrozen(_snapshot, SelectionPrompt);
            _isOverlayVisible = true;
            if (!_platform.MouseCapture.StartDrag(OnSelectionDown, OnSelectionMove, OnSelectionUp))
            {
                AbortSelection(TranslationStatus.SelectionFailed);
            }
        }
        catch (Exception exception)
        {
            // Сбой не должен ронять приложение: пользователь нажмёт ещё раз.
            _detail = $"{exception.GetType().Name}: {exception.Message}";
            AbortSelection(TranslationStatus.ReadFailed);
        }
    }

    private void AbortSelection(TranslationStatus status)
    {
        _isSelecting = false;
        _isDragging = false;
        _platform.MouseCapture.Stop();
        HideOverlay();
        ReleaseSnapshot();
        Report(status);
    }

    private void ReleaseSnapshot()
    {
        _snapshot?.Dispose();
        _snapshot = null;
    }

    private static TranslationStatus ToStatus(TextReadStatus status) => status switch
    {
        TextReadStatus.PermissionMissing => TranslationStatus.ScreenPermissionMissing,
        TextReadStatus.EmptyCapture => TranslationStatus.EmptyCapture,
        _ => TranslationStatus.ReadFailed,
    };

    private bool CanUseMouse() => _engine.Snapshot.State is not (EngineState.Acting or EngineState.PickingPoint or EngineState.RecordingMacro);

    /// <summary>Вызывается из системного перехвата мыши, поэтому не должен задерживаться.</summary>
    private bool OnSelectionDown(ScreenPoint point)
    {
        if (!_isSelecting)
        {
            return false;
        }

        // Кадр застыл, поэтому сверяемся с границами окна на момент снимка.
        var foreground = _platform.Windows.GetForegroundWindow();
        if (!_platform.Windows.FindRobloxWindows().Contains(foreground) || !_selectionBounds.Contains(point))
        {
            return false;
        }

        _dragStart = point;
        _dragCurrent = point;
        _isDragging = true;
        return true;
    }

    private void OnSelectionMove(ScreenPoint point)
    {
        _dragCurrent = point;
        if (!_isDragUpdatePending)
        {
            _isDragUpdatePending = true;
            _ = UpdateDragAsync();
        }
    }

    private void OnSelectionUp(ScreenPoint point)
    {
        _dragCurrent = point;
        _ = FinishSelectionAsync();
    }

    /// <summary>Много событий движения сливаются в одно обновление рамки на проход цикла интерфейса.</summary>
    private async Task UpdateDragAsync()
    {
        await _clock.Yield();
        _isDragUpdatePending = false;
        if (_isDragging)
        {
            _overlay.ShowSelection(DraggedArea());
            _isOverlayVisible = true;
        }
    }

    private async Task FinishSelectionAsync()
    {
        await _clock.Yield();
        var area = DraggedArea();
        var bounds = _selectionBounds;
        var snapshot = _snapshot;
        _snapshot = null;
        _isSelecting = false;
        _isDragging = false;
        HideOverlay();

        using (snapshot)
        {
            if (area.Width < MinAreaSize || area.Height < MinAreaSize)
            {
                Report(TranslationStatus.AreaTooSmall);
                return;
            }

            _engine.UpdateSettings(settings => settings with { TranslationArea = WindowArea.FromScreen(area, bounds) });
            await ReadAndTranslateAsync(isUpdate: false, snapshot, area);
        }
    }

    private ScreenRect DraggedArea()
    {
        var bounds = _selectionBounds;
        var left = Math.Clamp(Math.Min(_dragStart.X, _dragCurrent.X), bounds.X, bounds.X + bounds.Width);
        var top = Math.Clamp(Math.Min(_dragStart.Y, _dragCurrent.Y), bounds.Y, bounds.Y + bounds.Height);
        var right = Math.Clamp(Math.Max(_dragStart.X, _dragCurrent.X), bounds.X, bounds.X + bounds.Width);
        var bottom = Math.Clamp(Math.Max(_dragStart.Y, _dragCurrent.Y), bounds.Y, bounds.Y + bounds.Height);
        return new ScreenRect(left, top, right - left, bottom - top);
    }

    private async Task ReadAndTranslateAsync(bool isUpdate, ScreenSnapshot? snapshot = null, ScreenRect? snapshotArea = null)
    {
        var settings = Settings;
        if (settings.TranslationArea is not { } selected)
        {
            BeginSelection();
            return;
        }

        if (!_platform.TextReader.IsPermissionGranted)
        {
            _platform.TextReader.RequestPermission();
            Report(TranslationStatus.ScreenPermissionMissing);
            return;
        }

        _isBusy = true;
        using var cancel = new CancellationTokenSource();
        _readCancel = cancel;
        try
        {
            if (!isUpdate)
            {
                SetStatus(TranslationStatus.Reading);
            }

            ScreenRect bounds;
            ScreenRect area;
            TextReadResult read;
            if (snapshot is not null && snapshotArea is { } frozenArea)
            {
                bounds = snapshot.Bounds;
                area = frozenArea;
                read = await _platform.TextReader.ReadAsync(snapshot, area, cancel.Token);
            }
            else
            {
                var window = await PrepareWindowAsync(isUpdate);
                if (window is null || _platform.Windows.GetBounds(window.Value) is not { } liveBounds)
                {
                    return;
                }

                bounds = liveBounds;
                area = selected.ToScreen(bounds);
                if (_isOverlayVisible && TextLayout.Overlaps(_shownPanel, area))
                {
                    HideOverlay();
                    await _clock.Delay(OverlayHideDelay);
                }

                read = await _platform.TextReader.ReadAsync(area, cancel.Token);
            }

            if (cancel.IsCancellationRequested)
            {
                return;
            }

            if (read.Status != TextReadStatus.Success)
            {
                _detail = read.Detail;
                Report(ToStatus(read.Status));
                return;
            }

            await ShowResultAsync(TextLayout.ToText(read.Lines), area, bounds, isUpdate, settings.DeepLKey, cancel.Token);
        }
        catch (Exception exception)
        {
            // Сбой не должен ронять приложение: пользователь нажмёт ещё раз.
            _detail = $"{exception.GetType().Name}: {exception.Message}";
            Report(TranslationStatus.ReadFailed);
        }
        finally
        {
            _readCancel = null;
            _isBusy = false;
            _nextRefresh = _clock.Now + RefreshInterval;
            Changed?.Invoke();
        }
    }

    /// <summary>Возвращает окно Roblox на переднем плане. Обновление по таймеру окна не трогает: оно просто подождёт.</summary>
    private async Task<WindowHandle?> PrepareWindowAsync(bool isUpdate)
    {
        var windows = _platform.Windows.FindRobloxWindows();
        if (windows.Count == 0)
        {
            Report(TranslationStatus.RobloxMissing);
            return null;
        }

        var foreground = _platform.Windows.GetForegroundWindow();
        if (windows.Contains(foreground))
        {
            return foreground;
        }

        if (isUpdate)
        {
            return null;
        }

        var target = windows[0];
        BringForward(target);
        await _clock.Delay(_platform.WindowSettleDelay);
        if (_platform.Windows.GetForegroundWindow() != target)
        {
            Report(TranslationStatus.RobloxNotInFront);
            return null;
        }

        return target;
    }

    private async Task ShowResultAsync(string text, ScreenRect area, ScreenRect bounds, bool isUpdate, string deepLKey, CancellationToken cancellation)
    {
        if (text.Length == 0)
        {
            if (isUpdate)
            {
                // Диалог закрылся: прячем подпись, но продолжаем следить за областью.
                _source = "";
                _shownText = "";
                HideOverlay();
                return;
            }

            _source = "";
            _translated = "";
            Report(TranslationStatus.NoText);
            return;
        }

        if (isUpdate && text == _source && _shownText.Length > 0)
        {
            // Подпись могли спрятать на время снимка: текст тот же, поэтому просто возвращаем её.
            if (!_isOverlayVisible)
            {
                ShowOnOverlay(area, bounds, _shownText, _shownIsError);
            }

            return;
        }

        var translated = await _translate(text, deepLKey, cancellation);
        if (cancellation.IsCancellationRequested)
        {
            return;
        }

        _source = text;
        _detail = "";
        _isActive = true;
        _messageUntil = null;
        _translated = translated ?? "";
        _shownIsError = translated is null;
        _shownText = translated ?? $"{text}\n\nПеревод сейчас недоступен: проверь интернет.";
        ShowOnOverlay(area, bounds, _shownText, _shownIsError);
        SetStatus(translated is null ? TranslationStatus.ShownOriginal : TranslationStatus.Shown);
    }

    private void ShowOnOverlay(ScreenRect area, ScreenRect bounds, string text, bool isError)
    {
        _shownPanel = _overlay.ShowText(area, bounds, text, isError);
        _shownWindow = bounds;
        _isOverlayVisible = true;
    }

    private void HideOverlay()
    {
        if (_isOverlayVisible)
        {
            _overlay.Hide();
            _isOverlayVisible = false;
        }
    }

    private void BringForward(WindowHandle window)
    {
        if (_platform.Windows.IsMinimized(window))
        {
            _platform.Windows.Restore(window);
        }

        _platform.Windows.RequestForeground(window);
    }

    private WindowHandle FindForegroundRoblox()
    {
        var foreground = _platform.Windows.GetForegroundWindow();
        return _platform.Windows.FindRobloxWindows().Contains(foreground) ? foreground : WindowHandle.None;
    }

    /// <summary>Ошибку видно и в самой игре: нажав сочетание, пользователь не смотрит на приложение.</summary>
    private void Report(TranslationStatus status)
    {
        _status = status;
        _isActive = false;
        _shownText = "";
        if (Settings.TranslationEnabled && _platform.Windows.FindRobloxWindows() is [var first, ..]
            && _platform.Windows.GetBounds(first) is { } bounds && MessageFor(status) is { } message)
        {
            var area = Settings.TranslationArea?.ToScreen(bounds) ?? new ScreenRect(bounds.X, bounds.Y, bounds.Width, 0);
            ShowOnOverlay(area, bounds, message, isError: true);
            _messageUntil = _clock.Now + MessageDuration;
        }

        Changed?.Invoke();
    }

    private void SetStatus(TranslationStatus status)
    {
        _status = status;
        Changed?.Invoke();
    }

    private static string? MessageFor(TranslationStatus status) => status switch
    {
        TranslationStatus.NoText => "Текст не найден в выбранной области",
        TranslationStatus.AreaTooSmall => "Область слишком маленькая, выбери её заново",
        TranslationStatus.EmptyCapture => "Снимок окна пустой. Переключи Roblox в оконный режим",
        TranslationStatus.ReadFailed => "Не удалось прочитать текст",
        TranslationStatus.RobloxNotInFront => "Не удалось вывести Roblox вперёд",
        _ => null,
    };

    private static TranslationStatus IdleStatus(AppSettings settings)
    {
        if (!settings.TranslationEnabled)
        {
            return TranslationStatus.Disabled;
        }

        if (settings.TranslationSelectEachTime)
        {
            return TranslationStatus.ReadyToSelect;
        }

        return settings.TranslationArea is null ? TranslationStatus.NeedArea : TranslationStatus.Ready;
    }

    private static bool IsIdleStatus(TranslationStatus status) =>
        status is TranslationStatus.Disabled or TranslationStatus.NeedArea or TranslationStatus.Ready or TranslationStatus.ReadyToSelect;
}
