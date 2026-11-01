using Avalonia.Input;
using RobloxAntiAfk.Core.Domain;

namespace RobloxAntiAfk.App.Presentation;

public static class HotkeyInput
{
    /// <summary>Возвращает null, пока нажат только модификатор: сочетание ещё не закончено.</summary>
    public static Hotkey? From(PhysicalKey key, KeyModifiers modifiers) => key switch
    {
        PhysicalKey.None
            or PhysicalKey.AltLeft or PhysicalKey.AltRight
            or PhysicalKey.ControlLeft or PhysicalKey.ControlRight
            or PhysicalKey.MetaLeft or PhysicalKey.MetaRight
            or PhysicalKey.ShiftLeft or PhysicalKey.ShiftRight
            or PhysicalKey.CapsLock or PhysicalKey.NumLock => null,
        _ => new Hotkey(
            key.ToString(),
            modifiers.HasFlag(KeyModifiers.Control),
            modifiers.HasFlag(KeyModifiers.Shift),
            modifiers.HasFlag(KeyModifiers.Alt),
            modifiers.HasFlag(KeyModifiers.Meta)),
    };
}
