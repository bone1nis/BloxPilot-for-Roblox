using System.Runtime.InteropServices;

namespace RobloxAntiAfk.Platform.Mac.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct CGPoint
{
    public double X;
    public double Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CGRect
{
    public double X;
    public double Y;
    public double Width;
    public double Height;
}

internal static unsafe partial class CoreGraphics
{
    public const uint EventLeftMouseDown = 1;
    public const uint EventLeftMouseUp = 2;
    public const uint EventRightMouseDown = 3;
    public const uint EventRightMouseUp = 4;
    public const uint EventMouseMoved = 5;
    public const uint EventTapDisabledByTimeout = 0xFFFFFFFE;
    public const uint EventTapDisabledByUserInput = 0xFFFFFFFF;
    public const uint AnyInputEvent = uint.MaxValue;
    public const int ButtonLeft = 0;
    public const int ButtonRight = 1;
    public const int CombinedSessionState = 0;
    public const int HidEventTap = 0;
    public const int SessionEventTap = 1;
    public const int HeadInsertEventTap = 0;
    public const int DefaultTapOptions = 0;
    public const uint WindowListOnScreenOnly = 1;
    public const uint WindowListExcludeDesktopElements = 16;

    private const string Library = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    [LibraryImport(Library)]
    public static partial nint CGWindowListCopyWindowInfo(uint options, uint relativeToWindow);

    [LibraryImport(Library)]
    public static partial byte CGRectMakeWithDictionaryRepresentation(nint dictionary, out CGRect rect);

    [LibraryImport(Library)]
    public static partial double CGEventSourceSecondsSinceLastEventType(int state, uint eventType);

    [LibraryImport(Library)]
    public static partial nint CGEventCreate(nint source);

    [LibraryImport(Library)]
    public static partial CGPoint CGEventGetLocation(nint cgEvent);

    [LibraryImport(Library)]
    public static partial nint CGEventCreateMouseEvent(nint source, uint type, CGPoint position, int button);

    [LibraryImport(Library)]
    public static partial nint CGEventCreateKeyboardEvent(nint source, ushort keyCode, byte isDown);

    [LibraryImport(Library)]
    public static partial void CGEventPost(int tap, nint cgEvent);

    [LibraryImport(Library)]
    public static partial nint CGEventTapCreate(
        int tap,
        int place,
        int options,
        ulong eventsOfInterest,
        delegate* unmanaged<nint, uint, nint, nint, nint> callback,
        nint userInfo);

    [LibraryImport(Library)]
    public static partial void CGEventTapEnable(nint tap, byte enable);
}
