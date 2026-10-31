using System.Runtime.InteropServices;

namespace RobloxAntiAfk.Platform.Mac.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct EventTypeSpec
{
    public uint EventClass;
    public uint EventKind;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EventHotKeyId
{
    public uint Signature;
    public uint Id;
}

/// <summary>Системные сочетания Carbon не требуют разрешений и не показывают приложению остальные нажатия.</summary>
internal static unsafe partial class Carbon
{
    public const uint KeyboardEventClass = 0x6B657962; // 'keyb'
    public const uint HotKeyPressedKind = 5;
    public const uint ControlModifier = 4096;
    public const uint OptionModifier = 2048;
    public const uint DirectObjectParameter = 0x2D2D2D2D; // '----'
    public const uint HotKeyIdType = 0x686B6964; // 'hkid'

    internal const string Library = "/System/Library/Frameworks/Carbon.framework/Carbon";

    [LibraryImport(Library)]
    public static partial nint GetApplicationEventTarget();

    [LibraryImport(Library)]
    public static partial int InstallEventHandler(
        nint target,
        delegate* unmanaged<nint, nint, nint, int> handler,
        nuint typeCount,
        EventTypeSpec* types,
        nint userData,
        out nint handlerRef);

    [LibraryImport(Library)]
    public static partial int RemoveEventHandler(nint handlerRef);

    [LibraryImport(Library)]
    public static partial int GetEventParameter(
        nint carbonEvent,
        uint name,
        uint desiredType,
        nint actualType,
        nuint bufferSize,
        nint actualSize,
        void* data);

    [LibraryImport(Library)]
    public static partial int RegisterEventHotKey(uint keyCode, uint modifiers, EventHotKeyId id, nint target, uint options, out nint hotKeyRef);

    [LibraryImport(Library)]
    public static partial int UnregisterEventHotKey(nint hotKeyRef);
}
