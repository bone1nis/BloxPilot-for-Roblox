using System.Runtime.InteropServices;
using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;

namespace RobloxAntiAfk.Platform.Windows;

/// <summary>
/// Сообщение о сочетании приходит в невидимое окно этого же потока, поэтому хук клавиатуры не нужен:
/// приложение не видит остальных нажатий. Цикл сообщений интерфейса обслуживает и это окно.
/// </summary>
internal sealed unsafe class WindowsHotkey(int id, Hotkey defaultBinding) : IGlobalHotkey
{
    private const string ClassName = "RobloxAntiAfkHotkey";

    internal static readonly IReadOnlyDictionary<string, uint> VirtualKeys = BuildVirtualKeys();

    private static readonly Dictionary<nint, Action> Handlers = [];

    private Hotkey? _custom;
    private nint _window;

    private Hotkey Binding => _custom ?? defaultBinding;

    public string Label => Binding.ToLabel(isMac: false);

    public void Bind(Hotkey? custom) => _custom = custom;

    public bool Start(Action onPressed)
    {
        Stop();
        if (!VirtualKeys.TryGetValue(Binding.Key, out var virtualKey))
        {
            return false;
        }

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
        if (NativeMethods.RegisterHotKey(_window, id, Modifiers(Binding) | NativeMethods.ModifierNoRepeat, virtualKey) == 0)
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

    private static uint Modifiers(Hotkey hotkey) =>
        (hotkey.Control ? NativeMethods.ModifierControl : 0)
        | (hotkey.Alt ? NativeMethods.ModifierAlt : 0)
        | (hotkey.Shift ? NativeMethods.ModifierShift : 0)
        | (hotkey.Meta ? NativeMethods.ModifierWin : 0);

    private static Dictionary<string, uint> BuildVirtualKeys()
    {
        var keys = new Dictionary<string, uint>
        {
            ["Backspace"] = 0x08, ["Tab"] = 0x09, ["Enter"] = 0x0D, ["Pause"] = 0x13, [Hotkey.EscapeKey] = 0x1B, ["Space"] = 0x20,
            ["PageUp"] = 0x21, ["PageDown"] = 0x22, ["End"] = 0x23, ["Home"] = 0x24,
            ["ArrowLeft"] = 0x25, ["ArrowUp"] = 0x26, ["ArrowRight"] = 0x27, ["ArrowDown"] = 0x28,
            ["PrintScreen"] = 0x2C, ["Insert"] = 0x2D, ["Delete"] = 0x2E, ["ScrollLock"] = 0x91,
            ["NumPadMultiply"] = 0x6A, ["NumPadAdd"] = 0x6B, ["NumPadSubtract"] = 0x6D, ["NumPadDecimal"] = 0x6E, ["NumPadDivide"] = 0x6F,
            ["Semicolon"] = 0xBA, ["Equal"] = 0xBB, ["Comma"] = 0xBC, ["Minus"] = 0xBD, ["Period"] = 0xBE, ["Slash"] = 0xBF,
            ["Backquote"] = 0xC0, ["BracketLeft"] = 0xDB, ["Backslash"] = 0xDC, ["BracketRight"] = 0xDD, ["Quote"] = 0xDE,
        };

        for (var letter = 'A'; letter <= 'Z'; letter++)
        {
            keys[letter.ToString()] = letter;
        }

        for (uint digit = 0; digit <= 9; digit++)
        {
            keys[$"Digit{digit}"] = 0x30 + digit;
            keys[$"NumPad{digit}"] = 0x60 + digit;
        }

        for (uint function = 1; function <= 24; function++)
        {
            keys[$"F{function}"] = 0x6F + function;
        }

        return keys;
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
