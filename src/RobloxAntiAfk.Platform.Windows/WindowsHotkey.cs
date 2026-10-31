using System.Runtime.InteropServices;
using RobloxAntiAfk.Core.Abstractions;

namespace RobloxAntiAfk.Platform.Windows;

/// <summary>
/// Сообщение о сочетании приходит в невидимое окно этого же потока, поэтому хук клавиатуры не нужен:
/// приложение не видит остальных нажатий. Цикл сообщений интерфейса обслуживает и это окно.
/// </summary>
internal sealed unsafe class WindowsHotkey(int id, uint virtualKey, string label) : IGlobalHotkey
{
    private const string ClassName = "RobloxAntiAfkHotkey";

    private static readonly Dictionary<nint, Action> Handlers = [];

    private nint _window;

    public string Label => label;

    public bool Start(Action onPressed)
    {
        Stop();
        var module = NativeMethods.GetModuleHandle(0);
        fixed (char* className = ClassName)
        {
            var windowClass = new WindowClass
            {
                Size = (uint)sizeof(WindowClass),
                WindowProcedure = &WindowProcedure,
                Instance = module,
                ClassName = className,
            };

            // Повторная регистрация класса вернёт 0, это не ошибка: дальше всё решит создание окна.
            NativeMethods.RegisterClassEx(&windowClass);
            _window = NativeMethods.CreateWindowEx(0, className, null, 0, 0, 0, 0, 0, NativeMethods.MessageOnlyParent, 0, module, 0);
        }

        if (_window == 0)
        {
            return false;
        }

        Handlers[_window] = onPressed;
        const uint modifiers = NativeMethods.ModifierControl | NativeMethods.ModifierShift | NativeMethods.ModifierNoRepeat;
        if (NativeMethods.RegisterHotKey(_window, id, modifiers, virtualKey) == 0)
        {
            Stop();
            return false;
        }

        return true;
    }

    public void Stop()
    {
        if (_window != 0)
        {
            Handlers.Remove(_window);
            NativeMethods.UnregisterHotKey(_window, id);
            NativeMethods.DestroyWindow(_window);
            _window = 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint WindowProcedure(nint window, uint message, nint wParam, nint lParam)
    {
        if (message == NativeMethods.MessageHotkey)
        {
            try
            {
                if (Handlers.TryGetValue(window, out var handler))
                {
                    handler();
                }
            }
            catch (Exception)
            {
                // Исключение не должно уйти в системный вызов: нажатие просто пропадёт.
            }

            return 0;
        }

        return NativeMethods.DefWindowProc(window, message, wParam, lParam);
    }
}
