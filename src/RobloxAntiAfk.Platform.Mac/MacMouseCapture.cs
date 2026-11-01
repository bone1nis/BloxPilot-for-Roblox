using System.Runtime.InteropServices;
using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Platform.Mac.Native;

namespace RobloxAntiAfk.Platform.Mac;

/// <summary>Перехват мыши через CGEventTap. Требует разрешения «Универсальный доступ».</summary>
internal sealed unsafe class MacMouseCapture : IMouseCapture
{
    private static MacMouseCapture? _active;

    private nint _tap;
    private nint _source;
    private bool _swallowMouseUp;
    private bool _swallowRightUp;
    private Func<ScreenPoint, bool>? _onMouseDown;
    private Action<ScreenPoint>? _onMouseMove;
    private Action<ScreenPoint>? _onMouseUp;
    private Func<ScreenPoint, bool>? _onRightDown;
    private Action? _onRightUp;

    public bool Start(Func<ScreenPoint, bool> onMouseDown, bool includeRightButton = false) =>
        Install(onMouseDown, null, null, includeRightButton ? onMouseDown : null, null);

    public bool StartDrag(Func<ScreenPoint, bool> onMouseDown, Action<ScreenPoint> onMouseMove, Action<ScreenPoint> onMouseUp, Action onRightClick) =>
        Install(onMouseDown, onMouseMove, onMouseUp, _ => true, onRightClick);

    private bool Install(
        Func<ScreenPoint, bool> onMouseDown,
        Action<ScreenPoint>? onMouseMove,
        Action<ScreenPoint>? onMouseUp,
        Func<ScreenPoint, bool>? onRightDown,
        Action? onRightUp)
    {
        Stop();
        ReleaseStoppedTap();

        const ulong mask = (1UL << (int)CoreGraphics.EventLeftMouseDown)
            | (1UL << (int)CoreGraphics.EventLeftMouseUp)
            | (1UL << (int)CoreGraphics.EventLeftMouseDragged)
            | (1UL << (int)CoreGraphics.EventRightMouseDown)
            | (1UL << (int)CoreGraphics.EventRightMouseUp);
        _tap = CoreGraphics.CGEventTapCreate(
            CoreGraphics.SessionEventTap,
            CoreGraphics.HeadInsertEventTap,
            CoreGraphics.DefaultTapOptions,
            mask,
            &TapCallback,
            0);
        if (_tap == 0)
        {
            return false;
        }

        _source = CoreFoundation.CFMachPortCreateRunLoopSource(0, _tap, 0);
        CoreFoundation.CFRunLoopAddSource(CoreFoundation.CFRunLoopGetMain(), _source, CoreFoundation.RunLoopCommonModes);
        CoreGraphics.CGEventTapEnable(_tap, 1);
        _onMouseDown = onMouseDown;
        _onMouseMove = onMouseMove;
        _onMouseUp = onMouseUp;
        _onRightDown = onRightDown;
        _onRightUp = onRightUp;
        _active = this;
        return true;
    }

    /// <summary>Сам объект перехвата освобождается позже: Stop может быть вызван из его же обратного вызова.</summary>
    public void Stop()
    {
        if (_active != this)
        {
            return;
        }

        CoreGraphics.CGEventTapEnable(_tap, 0);
        CoreFoundation.CFRunLoopRemoveSource(CoreFoundation.CFRunLoopGetMain(), _source, CoreFoundation.RunLoopCommonModes);
        _onMouseDown = null;
        _onMouseMove = null;
        _onMouseUp = null;
        _onRightDown = null;
        _onRightUp = null;
        _swallowMouseUp = false;
        _swallowRightUp = false;
        _active = null;
    }

    private void ReleaseStoppedTap()
    {
        if (_tap == 0)
        {
            return;
        }

        CoreFoundation.CFMachPortInvalidate(_tap);
        CoreFoundation.CFRelease(_source);
        CoreFoundation.CFRelease(_tap);
        _tap = 0;
        _source = 0;
    }

    [UnmanagedCallersOnly]
    private static nint TapCallback(nint proxy, uint type, nint cgEvent, nint userInfo)
    {
        try
        {
            if (_active?.Handle(type, cgEvent) == true)
            {
                return 0;
            }
        }
        catch (Exception)
        {
            // Исключение не должно уйти в системный вызов: клик просто пройдёт дальше.
        }

        return cgEvent;
    }

    private bool Handle(uint type, nint cgEvent)
    {
        // Клики самого приложения, например действие по расписанию, сюда не относятся.
        if (type is not (CoreGraphics.EventTapDisabledByTimeout or CoreGraphics.EventTapDisabledByUserInput)
            && CoreGraphics.CGEventGetIntegerValueField(cgEvent, CoreGraphics.SourceProcessIdField) == Environment.ProcessId)
        {
            return false;
        }

        switch (type)
        {
            case CoreGraphics.EventTapDisabledByTimeout:
            case CoreGraphics.EventTapDisabledByUserInput:
                CoreGraphics.CGEventTapEnable(_tap, 1);
                return false;
            case CoreGraphics.EventLeftMouseDown when _onMouseDown is { } handler:
                var location = CoreGraphics.CGEventGetLocation(cgEvent);
                _swallowMouseUp = handler(new ScreenPoint(location.X, location.Y));
                return _swallowMouseUp;
            case CoreGraphics.EventLeftMouseDragged when _swallowMouseUp && _onMouseMove is { } onMove:
                var dragged = CoreGraphics.CGEventGetLocation(cgEvent);
                onMove(new ScreenPoint(dragged.X, dragged.Y));
                return false;
            case CoreGraphics.EventLeftMouseUp when _swallowMouseUp:
                var onUp = _onMouseUp;
                var released = CoreGraphics.CGEventGetLocation(cgEvent);
                Stop();
                onUp?.Invoke(new ScreenPoint(released.X, released.Y));
                return true;
            case CoreGraphics.EventRightMouseDown when _onRightDown is { } rightHandler:
                var rightLocation = CoreGraphics.CGEventGetLocation(cgEvent);
                _swallowRightUp = rightHandler(new ScreenPoint(rightLocation.X, rightLocation.Y));
                return _swallowRightUp;
            case CoreGraphics.EventRightMouseUp when _swallowRightUp:
                var onRightUp = _onRightUp;
                Stop();
                onRightUp?.Invoke();
                return true;
            default:
                return false;
        }
    }
}
