using System.Runtime.InteropServices;
using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;

namespace RobloxAntiAfk.Platform.Windows;

/// <summary>Хуки клавиатуры и мыши стоят только во время записи макроса и ничего не блокируют.</summary>
internal sealed unsafe class WindowsInputRecorder : IInputRecorder
{
    private static WindowsInputRecorder? _active;

    private nint _keyboardHook;
    private nint _mouseHook;
    private Action<RawInputEvent>? _onInput;

    public bool Start(Action<RawInputEvent> onInput)
    {
        Stop();
        var module = NativeMethods.GetModuleHandle(0);
        _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.HookKeyboardLowLevel, &KeyboardCallback, module, 0);
        _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.HookMouseLowLevel, &MouseCallback, module, 0);
        if (_keyboardHook == 0 || _mouseHook == 0)
        {
            Stop();
            return false;
        }

        _onInput = onInput;
        _active = this;
        return true;
    }

    public void Stop()
    {
        if (_keyboardHook != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = 0;
        }

        if (_mouseHook != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = 0;
        }

        _onInput = null;
        if (_active == this)
        {
            _active = null;
        }
    }

    [UnmanagedCallersOnly]
    private static nint KeyboardCallback(int code, nint message, nint data)
    {
        try
        {
            if (code >= 0)
            {
                _active?.HandleKeyboard(message, *(KeyboardHookData*)data);
            }
        }
        catch (Exception)
        {
            // Исключение не должно уйти в системный вызов: этот ввод просто не попадёт в запись.
        }

        return NativeMethods.CallNextHookEx(0, code, message, data);
    }

    [UnmanagedCallersOnly]
    private static nint MouseCallback(int code, nint message, nint data)
    {
        try
        {
            if (code >= 0)
            {
                _active?.HandleMouse(message, *(MouseHookData*)data);
            }
        }
        catch (Exception)
        {
            // Исключение не должно уйти в системный вызов: этот ввод просто не попадёт в запись.
        }

        return NativeMethods.CallNextHookEx(0, code, message, data);
    }

    private void HandleKeyboard(nint message, in KeyboardHookData data)
    {
        if ((data.Flags & NativeMethods.HookKeyInjected) != 0
            || !WindowsInputSimulator.TryGetKey((ushort)data.ScanCode, (data.Flags & NativeMethods.HookKeyExtended) != 0, out var key))
        {
            return;
        }

        var isDown = message is NativeMethods.MessageKeyDown or NativeMethods.MessageSystemKeyDown;
        _onInput?.Invoke(new RawInputEvent(MacroStepKind.Key, isDown, key));
    }

    private void HandleMouse(nint message, in MouseHookData data)
    {
        if ((data.Flags & NativeMethods.HookMouseInjected) != 0)
        {
            return;
        }

        // Координаты берём как при выборе точки: так они совпадут с GetWindowRect при любом масштабе.
        NativeMethods.GetCursorPos(out var cursor);
        var point = new ScreenPoint(cursor.X, cursor.Y);
        RawInputEvent? input = message switch
        {
            NativeMethods.MessageMouseMove => new RawInputEvent(MacroStepKind.MouseMove, Point: point),
            NativeMethods.MessageLeftButtonDown => new RawInputEvent(MacroStepKind.MouseButton, true, Button: MouseButton.Left, Point: point),
            NativeMethods.MessageLeftButtonUp => new RawInputEvent(MacroStepKind.MouseButton, false, Button: MouseButton.Left, Point: point),
            NativeMethods.MessageRightButtonDown => new RawInputEvent(MacroStepKind.MouseButton, true, Button: MouseButton.Right, Point: point),
            NativeMethods.MessageRightButtonUp => new RawInputEvent(MacroStepKind.MouseButton, false, Button: MouseButton.Right, Point: point),
            _ => null,
        };

        if (input is { } value)
        {
            _onInput?.Invoke(value);
        }
    }
}
