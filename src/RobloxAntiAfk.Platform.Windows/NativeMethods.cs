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

[StructLayout(LayoutKind.Sequential)]
internal struct KeyboardHookData
{
    public uint VirtualKey;
    public uint ScanCode;
    public uint Flags;
    public uint Time;
    public nint ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MouseHookData
{
    public NativePoint Point;
    public uint MouseData;
    public uint Flags;
    public uint Time;
    public nint ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct WindowClass
{
    public uint Size;
    public uint Style;
    public delegate* unmanaged<nint, uint, nint, nint, nint> WindowProcedure;
    public int ClassExtra;
    public int WindowExtra;
    public nint Instance;
    public nint Icon;
    public nint Cursor;
    public nint Background;
    public char* MenuName;
    public char* ClassName;
    public nint SmallIcon;
}

[StructLayout(LayoutKind.Sequential)]
internal struct BitmapInfoHeader
{
    public uint Size;
    public int Width;
    public int Height;
    public ushort Planes;
    public ushort BitCount;
    public uint Compression;
    public uint SizeImage;
    public int XPixelsPerMeter;
    public int YPixelsPerMeter;
    public uint ColorsUsed;
    public uint ColorsImportant;
}

internal static unsafe partial class NativeMethods
{
    public const uint InputMouse = 0;
    public const uint InputKeyboard = 1;
    public const uint MouseLeftDown = 0x0002;
    public const uint MouseLeftUp = 0x0004;
    public const uint MouseRightDown = 0x0008;
    public const uint MouseRightUp = 0x0010;
    public const uint KeyEventExtended = 0x0001;
    public const uint KeyEventUp = 0x0002;
    public const uint KeyEventScanCode = 0x0008;
    public const int ShowMinimized = 6;
    public const int ShowRestored = 9;
    public const int HookKeyboardLowLevel = 13;
    public const int HookMouseLowLevel = 14;
    public const uint HookKeyExtended = 0x01;
    public const uint HookKeyInjected = 0x10;
    public const uint HookMouseInjected = 0x01;
    public const nint MessageKeyDown = 0x0100;
    public const nint MessageSystemKeyDown = 0x0104;
    public const nint MessageMouseMove = 0x0200;
    public const nint MessageLeftButtonDown = 0x0201;
    public const nint MessageLeftButtonUp = 0x0202;
    public const nint MessageRightButtonDown = 0x0204;
    public const nint MessageRightButtonUp = 0x0205;
    public const uint MessageHotkey = 0x0312;
    public const uint ModifierControl = 0x0002;
    public const uint ModifierShift = 0x0004;
    public const uint ModifierNoRepeat = 0x4000;
    public const uint VirtualKeyF8 = 0x77;
    public const uint VirtualKeyX = 0x58;
    public const uint VirtualKeyEscape = 0x1B;
    public const int ExtendedStyleIndex = -20;
    public const nint StyleTransparent = 0x00000020;
    public const nint StyleToolWindow = 0x00000080;
    public const nint StyleLayered = 0x00080000;
    public const nint StyleNoActivate = 0x08000000;
    public const uint LayeredUseAlpha = 0x00000002;
    public const uint RasterCopy = 0x00CC0020;
    public const uint RasterCaptureLayered = 0x40000000;
    public const nint MessageOnlyParent = -3;
    public const uint ExecutionContinuous = 0x80000000;
    public const uint ExecutionSystemRequired = 0x00000001;
    public const uint ExecutionDisplayRequired = 0x00000002;

    private const string User32 = "user32.dll";
    private const string Kernel32 = "kernel32.dll";
    private const string Gdi32 = "gdi32.dll";

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

    [LibraryImport(User32, EntryPoint = "RegisterClassExW")]
    public static partial ushort RegisterClassEx(WindowClass* windowClass);

    [LibraryImport(User32, EntryPoint = "CreateWindowExW")]
    public static partial nint CreateWindowEx(
        uint extendedStyle,
        char* className,
        char* title,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [LibraryImport(User32)]
    public static partial int DestroyWindow(nint window);

    [LibraryImport(User32, EntryPoint = "DefWindowProcW")]
    public static partial nint DefWindowProc(nint window, uint message, nint wParam, nint lParam);

    [LibraryImport(User32)]
    public static partial int RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    [LibraryImport(User32)]
    public static partial int UnregisterHotKey(nint window, int id);

    [LibraryImport(User32, EntryPoint = "GetWindowLongPtrW")]
    public static partial nint GetWindowLongPtr(nint window, int index);

    [LibraryImport(User32, EntryPoint = "SetWindowLongPtrW")]
    public static partial nint SetWindowLongPtr(nint window, int index, nint value);

    [LibraryImport(User32)]
    public static partial int SetLayeredWindowAttributes(nint window, uint colorKey, byte alpha, uint flags);

    [LibraryImport(User32)]
    public static partial nint WindowFromPoint(NativePoint point);

    [LibraryImport(User32)]
    public static partial nint GetDC(nint window);

    [LibraryImport(User32)]
    public static partial int ReleaseDC(nint window, nint deviceContext);

    [LibraryImport(Gdi32)]
    public static partial nint CreateCompatibleDC(nint deviceContext);

    [LibraryImport(Gdi32)]
    public static partial nint CreateCompatibleBitmap(nint deviceContext, int width, int height);

    [LibraryImport(Gdi32)]
    public static partial nint SelectObject(nint deviceContext, nint graphicsObject);

    [LibraryImport(Gdi32)]
    public static partial int BitBlt(nint target, int x, int y, int width, int height, nint source, int sourceX, int sourceY, uint operation);

    [LibraryImport(Gdi32)]
    public static partial int GetDIBits(nint deviceContext, nint bitmap, uint firstLine, uint lineCount, void* bits, BitmapInfoHeader* info, uint usage);

    [LibraryImport(Gdi32)]
    public static partial int DeleteObject(nint graphicsObject);

    [LibraryImport(Gdi32)]
    public static partial int DeleteDC(nint deviceContext);

    [LibraryImport(Kernel32)]
    public static partial uint GetTickCount();

    [LibraryImport(Kernel32)]
    public static partial uint SetThreadExecutionState(uint flags);

    [LibraryImport(Kernel32, EntryPoint = "GetModuleHandleW")]
    public static partial nint GetModuleHandle(nint name);
}
