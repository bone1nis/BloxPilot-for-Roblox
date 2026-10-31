using System.Runtime.InteropServices;
using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Platform.Mac.Native;

namespace RobloxAntiAfk.Platform.Mac;

/// <summary>Перехват клавиатуры и мыши через CGEventTap только на время записи. События проходят дальше без изменений.</summary>
internal sealed unsafe class MacInputRecorder : IInputRecorder
{
    private const ushort ShiftKeyCode = 56;
    private const ushort ControlKeyCode = 59;

    private static readonly Dictionary<ushort, GameKey> KeysByCode =
        MacInputSimulator.KeyCodes.ToDictionary(pair => pair.Value, pair => pair.Key);

    private static MacInputRecorder? _active;

    private nint _tap;
    private nint _source;
    private Action<RawInputEvent>? _onInput;

    public bool Start(Action<RawInputEvent> onInput)
    {
        Stop();
        ReleaseStoppedTap();

        const ulong mask = (1UL << (int)CoreGraphics.EventLeftMouseDown)
            | (1UL << (int)CoreGraphics.EventLeftMouseUp)
            | (1UL << (int)CoreGraphics.EventRightMouseDown)
            | (1UL << (int)CoreGraphics.EventRightMouseUp)
            | (1UL << (int)CoreGraphics.EventMouseMoved)
            | (1UL << (int)CoreGraphics.EventLeftMouseDragged)
            | (1UL << (int)CoreGraphics.EventRightMouseDragged)
            | (1UL << (int)CoreGraphics.EventKeyDown)
            | (1UL << (int)CoreGraphics.EventKeyUp)
            | (1UL << (int)CoreGraphics.EventFlagsChanged);
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
        _onInput = onInput;
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
        _onInput = null;
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
            _active?.Handle(type, cgEvent);
        }
        catch (Exception)
        {
            // Исключение не должно уйти в системный вызов: этот ввод просто не попадёт в запись.
        }

        return cgEvent;
    }

    private void Handle(uint type, nint cgEvent)
    {
        switch (type)
        {
            case CoreGraphics.EventTapDisabledByTimeout:
            case CoreGraphics.EventTapDisabledByUserInput:
                CoreGraphics.CGEventTapEnable(_tap, 1);
                break;
            case CoreGraphics.EventKeyDown:
                SendKey(cgEvent, isDown: true);
                break;
            case CoreGraphics.EventKeyUp:
                SendKey(cgEvent, isDown: false);
                break;
            case CoreGraphics.EventFlagsChanged:
                SendModifier(cgEvent);
                break;
            case CoreGraphics.EventLeftMouseDown:
                SendButton(cgEvent, MouseButton.Left, isDown: true);
                break;
            case CoreGraphics.EventLeftMouseUp:
                SendButton(cgEvent, MouseButton.Left, isDown: false);
                break;
            case CoreGraphics.EventRightMouseDown:
                SendButton(cgEvent, MouseButton.Right, isDown: true);
                break;
            case CoreGraphics.EventRightMouseUp:
                SendButton(cgEvent, MouseButton.Right, isDown: false);
                break;
            case CoreGraphics.EventMouseMoved:
            case CoreGraphics.EventLeftMouseDragged:
            case CoreGraphics.EventRightMouseDragged:
                _onInput?.Invoke(new RawInputEvent(MacroStepKind.MouseMove, Point: Location(cgEvent)));
                break;
        }
    }

    private void SendKey(nint cgEvent, bool isDown)
    {
        if (KeysByCode.TryGetValue(KeyCode(cgEvent), out var key))
        {
            _onInput?.Invoke(new RawInputEvent(MacroStepKind.Key, isDown, key));
        }
    }

    /// <summary>Нажатие Shift и Control приходит как смена флагов, поэтому состояние читаем из них.</summary>
    private void SendModifier(nint cgEvent)
    {
        var flags = CoreGraphics.CGEventGetFlags(cgEvent);
        switch (KeyCode(cgEvent))
        {
            case ShiftKeyCode:
                _onInput?.Invoke(new RawInputEvent(MacroStepKind.Key, (flags & CoreGraphics.FlagShift) != 0, GameKey.Shift));
                break;
            case ControlKeyCode:
                _onInput?.Invoke(new RawInputEvent(MacroStepKind.Key, (flags & CoreGraphics.FlagControl) != 0, GameKey.Control));
                break;
        }
    }

    private void SendButton(nint cgEvent, MouseButton button, bool isDown) =>
        _onInput?.Invoke(new RawInputEvent(MacroStepKind.MouseButton, isDown, Button: button, Point: Location(cgEvent)));

    private static ushort KeyCode(nint cgEvent) =>
        (ushort)CoreGraphics.CGEventGetIntegerValueField(cgEvent, CoreGraphics.KeyboardKeyCodeField);

    private static ScreenPoint Location(nint cgEvent)
    {
        var location = CoreGraphics.CGEventGetLocation(cgEvent);
        return new ScreenPoint(location.X, location.Y);
    }
}
