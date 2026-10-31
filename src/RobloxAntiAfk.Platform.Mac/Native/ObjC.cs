using System.Runtime.InteropServices;

namespace RobloxAntiAfk.Platform.Mac.Native;

/// <summary>Минимальный мост к Objective-C: NSWorkspace, NSRunningApplication и NSProcessInfo есть только там.</summary>
internal static partial class ObjC
{
    private const string Library = "/usr/lib/libobjc.A.dylib";

    private static readonly nint AppKit = NativeLibrary.Load("/System/Library/Frameworks/AppKit.framework/AppKit");

    /// <summary>Держит AppKit загруженным, иначе его классов может не быть в рантайме.</summary>
    public static bool EnsureAppKitLoaded() => AppKit != 0;

    public static nint Class(string name) => objc_getClass(name);

    public static nint Selector(string name) => sel_registerName(name);

    public static nint Send(nint target, string selector) => SendPointer(target, Selector(selector));

    public static nint Send(nint target, string selector, nint argument) => SendPointer(target, Selector(selector), argument);

    public static nint Send(nint target, string selector, nint first, nint second) => SendPointer(target, Selector(selector), first, second);

    public static bool Responds(nint target, string selector) => SendByte(target, Selector("respondsToSelector:"), Selector(selector)) != 0;

    public static float SendFloat(nint target, string selector) => SendSingle(target, Selector(selector));

    /// <summary>На Intel структуры такого размера возвращает отдельная функция, на Apple Silicon её нет.</summary>
    public static CGRect SendRect(nint target, string selector) => RuntimeInformation.ProcessArchitecture == Architecture.X64
        ? SendRectStret(target, Selector(selector))
        : SendRectDirect(target, Selector(selector));

    public static void SetRegionOfInterest(nint request, CGRect region) => SendRectArgument(request, Selector("setRegionOfInterest:"), region);

    public static string? ToText(nint text) => text == 0 ? null : Marshal.PtrToStringUTF8(Send(text, "UTF8String"));

    public static int SendInt(nint target, string selector) => SendInt32(target, Selector(selector));

    public static bool SendBool(nint target, string selector) => SendByte(target, Selector(selector)) != 0;

    public static long SendLong(nint target, string selector) => SendInt64(target, Selector(selector));

    public static nint SendIndex(nint target, string selector, long index) => SendPointerIndex(target, Selector(selector), index);

    public static nint BeginActivity(nint processInfo, ulong options, nint reason) =>
        SendPointerActivity(processInfo, Selector("beginActivityWithOptions:reason:"), options, reason);

    [LibraryImport(Library)]
    public static partial nint objc_autoreleasePoolPush();

    [LibraryImport(Library)]
    public static partial void objc_autoreleasePoolPop(nint pool);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint objc_getClass(string name);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint sel_registerName(string name);

    [LibraryImport(Library, EntryPoint = "objc_msgSend")]
    private static partial nint SendPointer(nint target, nint selector);

    [LibraryImport(Library, EntryPoint = "objc_msgSend")]
    private static partial nint SendPointer(nint target, nint selector, nint argument);

    [LibraryImport(Library, EntryPoint = "objc_msgSend")]
    private static partial nint SendPointer(nint target, nint selector, nint first, nint second);

    [LibraryImport(Library, EntryPoint = "objc_msgSend")]
    private static partial byte SendByte(nint target, nint selector, nint argument);

    [LibraryImport(Library, EntryPoint = "objc_msgSend")]
    private static partial float SendSingle(nint target, nint selector);

    [LibraryImport(Library, EntryPoint = "objc_msgSend")]
    private static partial CGRect SendRectDirect(nint target, nint selector);

    [LibraryImport(Library, EntryPoint = "objc_msgSend")]
    private static partial void SendRectArgument(nint target, nint selector, CGRect argument);

    [LibraryImport(Library, EntryPoint = "objc_msgSend_stret")]
    private static partial CGRect SendRectStret(nint target, nint selector);

    [LibraryImport(Library, EntryPoint = "objc_msgSend")]
    private static partial nint SendPointerIndex(nint target, nint selector, long index);

    [LibraryImport(Library, EntryPoint = "objc_msgSend")]
    private static partial nint SendPointerActivity(nint target, nint selector, ulong options, nint reason);

    [LibraryImport(Library, EntryPoint = "objc_msgSend")]
    private static partial int SendInt32(nint target, nint selector);

    [LibraryImport(Library, EntryPoint = "objc_msgSend")]
    private static partial long SendInt64(nint target, nint selector);

    [LibraryImport(Library, EntryPoint = "objc_msgSend")]
    private static partial byte SendByte(nint target, nint selector);
}
