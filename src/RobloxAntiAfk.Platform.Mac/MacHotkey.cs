using System.Runtime.InteropServices;
using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Platform.Mac.Native;

namespace RobloxAntiAfk.Platform.Mac;

internal sealed unsafe class MacHotkey(uint id, Hotkey defaultBinding) : IGlobalHotkey
{
    private const uint Signature = 0x52414146; // 'RAAF'

    internal static readonly IReadOnlyDictionary<string, uint> KeyCodes = new Dictionary<string, uint>
    {
        ["A"] = 0, ["S"] = 1, ["D"] = 2, ["F"] = 3, ["H"] = 4, ["G"] = 5, ["Z"] = 6, ["X"] = 7, ["C"] = 8, ["V"] = 9,
        ["B"] = 11, ["Q"] = 12, ["W"] = 13, ["E"] = 14, ["R"] = 15, ["Y"] = 16, ["T"] = 17, ["O"] = 31, ["U"] = 32,
        ["I"] = 34, ["P"] = 35, ["L"] = 37, ["J"] = 38, ["K"] = 40, ["N"] = 45, ["M"] = 46,
        ["Digit1"] = 18, ["Digit2"] = 19, ["Digit3"] = 20, ["Digit4"] = 21, ["Digit6"] = 22, ["Digit5"] = 23,
        ["Digit9"] = 25, ["Digit7"] = 26, ["Digit8"] = 28, ["Digit0"] = 29,
        ["Equal"] = 24, ["Minus"] = 27, ["BracketRight"] = 30, ["BracketLeft"] = 33, ["Quote"] = 39, ["Semicolon"] = 41,
        ["Backslash"] = 42, ["Comma"] = 43, ["Slash"] = 44, ["Period"] = 47, ["Backquote"] = 50,
        ["Enter"] = 36, ["Tab"] = 48, ["Space"] = 49, ["Backspace"] = 51, [Hotkey.EscapeKey] = 53,
        ["F1"] = 122, ["F2"] = 120, ["F3"] = 99, ["F4"] = 118, ["F5"] = 96, ["F6"] = 97, ["F7"] = 98, ["F8"] = 100,
        ["F9"] = 101, ["F10"] = 109, ["F11"] = 103, ["F12"] = 111, ["F13"] = 105, ["F14"] = 107, ["F15"] = 113,
        ["F16"] = 106, ["F17"] = 64, ["F18"] = 79, ["F19"] = 80, ["F20"] = 90,
        ["Insert"] = 114, ["Home"] = 115, ["PageUp"] = 116, ["Delete"] = 117, ["End"] = 119, ["PageDown"] = 121,
        ["ArrowLeft"] = 123, ["ArrowRight"] = 124, ["ArrowDown"] = 125, ["ArrowUp"] = 126,
        ["NumPad0"] = 82, ["NumPad1"] = 83, ["NumPad2"] = 84, ["NumPad3"] = 85, ["NumPad4"] = 86,
        ["NumPad5"] = 87, ["NumPad6"] = 88, ["NumPad7"] = 89, ["NumPad8"] = 91, ["NumPad9"] = 92,
        ["NumPadDecimal"] = 65, ["NumPadMultiply"] = 67, ["NumPadAdd"] = 69, ["NumPadDivide"] = 75,
        ["NumPadEnter"] = 76, ["NumPadSubtract"] = 78, ["NumPadEqual"] = 81,
    };

    private static readonly Dictionary<uint, Action> Handlers = [];

    private static bool _isHandlerInstalled;

    private Hotkey? _custom;
    private nint _hotKey;

    private Hotkey Binding => _custom ?? defaultBinding;

    public string Label => Binding.ToLabel(isMac: true);

    public void Bind(Hotkey? custom) => _custom = custom;

    public bool Start(Action onPressed)
    {
        Stop();
        var target = Carbon.GetApplicationEventTarget();
        if (!KeyCodes.TryGetValue(Binding.Key, out var keyCode) || !EnsureHandlerInstalled(target))
        {
            return false;
        }

        var hotKeyId = new EventHotKeyId { Signature = Signature, Id = id };
        if (Carbon.RegisterEventHotKey(keyCode, Modifiers(Binding), hotKeyId, target, 0, out _hotKey) != 0)
        {
            _hotKey = 0;
            return false;
        }

        Handlers[id] = onPressed;
        return true;
    }

    public void Stop()
    {
        Handlers.Remove(id);
        if (_hotKey != 0)
        {
            Carbon.UnregisterEventHotKey(_hotKey);
            _hotKey = 0;
        }
    }

    /// <summary>Обработчик один на все сочетания: повторная установка того же обработчика система отклоняет.</summary>
    private static bool EnsureHandlerInstalled(nint target)
    {
        if (_isHandlerInstalled)
        {
            return true;
        }

        var type = new EventTypeSpec { EventClass = Carbon.KeyboardEventClass, EventKind = Carbon.HotKeyPressedKind };
        _isHandlerInstalled = Carbon.InstallEventHandler(target, &HandlePressed, 1, &type, 0, out _) == 0;
        return _isHandlerInstalled;
    }

    private static uint Modifiers(Hotkey hotkey) =>
        (hotkey.Control ? Carbon.ControlModifier : 0)
        | (hotkey.Alt ? Carbon.OptionModifier : 0)
        | (hotkey.Shift ? Carbon.ShiftModifier : 0)
        | (hotkey.Meta ? Carbon.CommandModifier : 0);

    [UnmanagedCallersOnly]
    private static int HandlePressed(nint nextHandler, nint carbonEvent, nint userData)
    {
        try
        {
            EventHotKeyId pressed;
            if (Carbon.GetEventParameter(carbonEvent, Carbon.DirectObjectParameter, Carbon.HotKeyIdType, 0, (nuint)sizeof(EventHotKeyId), 0, &pressed) == 0
                && Handlers.TryGetValue(pressed.Id, out var handler))
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
}
