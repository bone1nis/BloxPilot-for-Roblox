using System.Runtime.InteropServices;
using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;

namespace RobloxAntiAfk.Platform.Windows;

/// <summary>
/// Низкоуровневый хук мыши. Его вызывает цикл сообщений потока, который установил хук,
/// поэтому Start и Stop должны вызываться из потока интерфейса.
/// </summary>
internal sealed unsafe class WindowsMouseCapture : IMouseCapture
{
    private static WindowsMouseCapture? _active;

    private nint _hook;
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
        var module = NativeMethods.GetModuleHandle(0);
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.HookMouseLowLevel, &HookCallback, module, 0);
        if (_hook == 0)
        {
            return false;
        }

        _onMouseDown = onMouseDown;
        _onMouseMove = onMouseMove;
        _onMouseUp = onMouseUp;
        _onRightDown = onRightDown;
        _onRightUp = onRightUp;
        _active = this;
        return true;
    }

    public void Stop()
    {
        if (_hook != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = 0;
        }

        _onMouseDown = null;
        _onMouseMove = null;
        _onMouseUp = null;
        _onRightDown = null;
        _onRightUp = null;
        _swallowMouseUp = false;
        _swallowRightUp = false;
        if (_active == this)
        {
            _active = null;
        }
    }

    [UnmanagedCallersOnly]
    private static nint HookCallback(int code, nint message, nint data)
    {
        try
        {
            if (code >= 0 && (((MouseHookData*)data)->Flags & NativeMethods.HookMouseInjected) == 0 && _active?.Handle(message) == true)
            {
                return 1;
            }
        }
        catch (Exception)
        {
            // Исключение не должно уйти в системный вызов: клик просто пройдёт дальше.
        }

        return NativeMethods.CallNextHookEx(0, code, message, data);
    }

    private bool Handle(nint message)
    {
        if (message == NativeMethods.MessageLeftButtonDown && _onMouseDown is { } handler)
        {
            // Координаты берём здесь, а не из данных хука: так они совпадут с GetWindowRect при любом масштабе.
            NativeMethods.GetCursorPos(out var cursor);
            _swallowMouseUp = handler(new ScreenPoint(cursor.X, cursor.Y));
            return _swallowMouseUp;
        }

        if (message == NativeMethods.MessageMouseMove && _swallowMouseUp && _onMouseMove is { } onMove)
        {
            NativeMethods.GetCursorPos(out var cursor);
            onMove(new ScreenPoint(cursor.X, cursor.Y));
            return false;
        }

        if (message == NativeMethods.MessageLeftButtonUp && _swallowMouseUp)
        {
            var onUp = _onMouseUp;
            NativeMethods.GetCursorPos(out var cursor);
            Stop();
            onUp?.Invoke(new ScreenPoint(cursor.X, cursor.Y));
            return true;
        }

        if (message == NativeMethods.MessageRightButtonDown && _onRightDown is { } rightHandler)
        {
            NativeMethods.GetCursorPos(out var cursor);
            _swallowRightUp = rightHandler(new ScreenPoint(cursor.X, cursor.Y));
            return _swallowRightUp;
        }

        if (message == NativeMethods.MessageRightButtonUp && _swallowRightUp)
        {
            var onRightUp = _onRightUp;
            Stop();
            onRightUp?.Invoke();
            return true;
        }

        return false;
    }
}
