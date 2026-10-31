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
    private Func<ScreenPoint, bool>? _onMouseDown;

    public bool Start(Func<ScreenPoint, bool> onMouseDown)
    {
        Stop();
        ReleaseStoppedTap();

        const ulong mask = (1UL << (int)CoreGraphics.EventLeftMouseDown) | (1UL << (int)CoreGraphics.EventLeftMouseUp);
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
        _swallowMouseUp = false;
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
            case CoreGraphics.EventLeftMouseUp when _swallowMouseUp:
                Stop();
                return true;
            default:
                return false;
        }
    }
}
