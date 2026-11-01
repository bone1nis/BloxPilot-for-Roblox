using System.Runtime.InteropServices;
using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Platform.Mac.Native;

namespace RobloxAntiAfk.Platform.Mac;

internal sealed unsafe class MacHotkey(uint id, uint keyCode, string label, uint modifiers = Carbon.ControlModifier | Carbon.OptionModifier) : IGlobalHotkey
{
    public const uint KeyCodeA = 0;
    public const uint KeyCodeT = 17;
    public const uint KeyCodeEscape = 53;
    private const uint Signature = 0x52414146; // 'RAAF'

    private static readonly Dictionary<uint, Action> Handlers = [];

    private nint _handler;
    private nint _hotKey;

    public string Label => label;

    public bool Start(Action onPressed)
    {
        Stop();
        var target = Carbon.GetApplicationEventTarget();
        var type = new EventTypeSpec { EventClass = Carbon.KeyboardEventClass, EventKind = Carbon.HotKeyPressedKind };
        if (Carbon.InstallEventHandler(target, &HandlePressed, 1, &type, 0, out _handler) != 0)
        {
            _handler = 0;
            return false;
        }

        var hotKeyId = new EventHotKeyId { Signature = Signature, Id = id };
        if (Carbon.RegisterEventHotKey(keyCode, modifiers, hotKeyId, target, 0, out _hotKey) != 0)
        {
            _hotKey = 0;
            Stop();
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

        if (_handler != 0)
        {
            Carbon.RemoveEventHandler(_handler);
            _handler = 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int HandlePressed(nint nextHandler, nint carbonEvent, nint userData)
    {
        try
        {
            // Обработчик один на все сочетания: первый установленный получает их все.
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
