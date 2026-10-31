using System.Runtime.InteropServices;

namespace RobloxAntiAfk.Platform.Windows;

[StructLayout(LayoutKind.Sequential)]
internal struct NativePoint
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LastInputInfo
{
    public uint Size;
    public uint Time;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MouseInput
{
    public int Dx;
    public int Dy;
    public uint MouseData;
    public uint Flags;
    public uint Time;
    public nint ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KeyboardInput
{
    public ushort VirtualKey;
    public ushort ScanCode;
    public uint Flags;
    public uint Time;
    public nint ExtraInfo;
}

[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    [FieldOffset(0)] public MouseInput Mouse;
    [FieldOffset(0)] public KeyboardInput Keyboard;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Input
{
    public uint Type;
    public InputUnion Data;
}

internal static unsafe partial class NativeMethods
{
    public const uint InputMouse = 0;
    public const uint InputKeyboard = 1;
    public const uint MouseLeftDown = 0x0002;
    public const uint MouseLeftUp = 0x0004;
    public const uint MouseRightDown = 0x0008;
    public const uint MouseRightUp = 0x0010;
    public const uint KeyEventUp = 0x0002;
    public const uint KeyEventScanCode = 0x0008;
    public const int ShowMinimized = 6;
    public const int ShowRestored = 9;
    public const int HookMouseLowLevel = 14;
    public const nint MessageLeftButtonDown = 0x0201;
    public const nint MessageLeftButtonUp = 0x0202;
    public const uint ExecutionContinuous = 0x80000000;
    public const uint ExecutionSystemRequired = 0x00000001;
    public const uint ExecutionDisplayRequired = 0x00000002;

    private const string User32 = "user32.dll";
    private const string Kernel32 = "kernel32.dll";

    [LibraryImport(User32)]
    public static partial int EnumWindows(delegate* unmanaged<nint, nint, int> callback, nint parameter);

    [LibraryImport(User32)]
    public static partial int IsWindowVisible(nint window);

    [LibraryImport(User32, EntryPoint = "GetWindowTextW")]
    public static partial int GetWindowText(nint window, char* buffer, int capacity);

    [LibraryImport(User32)]
    public static partial uint GetWindowThreadProcessId(nint window, out uint processId);

    [LibraryImport(User32)]
    public static partial nint GetForegroundWindow();

    [LibraryImport(User32)]
    public static partial int SetForegroundWindow(nint window);

    [LibraryImport(User32)]
    public static partial int GetWindowRect(nint window, out NativeRect rect);

    [LibraryImport(User32)]
    public static partial int IsIconic(nint window);

    [LibraryImport(User32)]
    public static partial int ShowWindow(nint window, int command);

    [LibraryImport(User32)]
    public static partial int GetCursorPos(out NativePoint point);

    [LibraryImport(User32)]
    public static partial int SetCursorPos(int x, int y);

    [LibraryImport(User32)]
    public static partial uint SendInput(uint count, Input* inputs, int size);

    [LibraryImport(User32)]
    public static partial int GetLastInputInfo(ref LastInputInfo info);

    [LibraryImport(User32, EntryPoint = "SetWindowsHookExW")]
    public static partial nint SetWindowsHookEx(int hookType, delegate* unmanaged<int, nint, nint, nint> callback, nint module, uint threadId);

    [LibraryImport(User32)]
    public static partial int UnhookWindowsHookEx(nint hook);

    [LibraryImport(User32)]
    public static partial nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [LibraryImport(Kernel32)]
    public static partial uint GetTickCount();

    [LibraryImport(Kernel32)]
    public static partial uint SetThreadExecutionState(uint flags);

    [LibraryImport(Kernel32, EntryPoint = "GetModuleHandleW")]
    public static partial nint GetModuleHandle(nint name);
}
