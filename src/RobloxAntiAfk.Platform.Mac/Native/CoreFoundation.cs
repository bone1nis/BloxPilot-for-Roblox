using System.Runtime.InteropServices;

namespace RobloxAntiAfk.Platform.Mac.Native;

internal static partial class CoreFoundation
{
    private const string Library = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const uint Utf8Encoding = 0x08000100;
    private const int NumberSInt64 = 4;

    private static readonly nint Handle = NativeLibrary.Load(Library);

    public static nint BooleanTrue => Marshal.ReadIntPtr(NativeLibrary.GetExport(Handle, "kCFBooleanTrue"));

    public static nint RunLoopCommonModes => Marshal.ReadIntPtr(NativeLibrary.GetExport(Handle, "kCFRunLoopCommonModes"));

    public static nint DictionaryKeyCallBacks => NativeLibrary.GetExport(Handle, "kCFTypeDictionaryKeyCallBacks");

    public static nint DictionaryValueCallBacks => NativeLibrary.GetExport(Handle, "kCFTypeDictionaryValueCallBacks");

    public static nint CreateString(string value) => CFStringCreateWithCString(0, value, Utf8Encoding);

    public static long? GetNumber(nint number) =>
        number != 0 && CFNumberGetValue(number, NumberSInt64, out var value) != 0 ? value : null;

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint CFStringCreateWithCString(nint allocator, string value, uint encoding);

    [LibraryImport(Library)]
    private static partial byte CFNumberGetValue(nint number, int type, out long value);

    [LibraryImport(Library)]
    public static partial void CFRelease(nint value);

    [LibraryImport(Library)]
    public static partial long CFArrayGetCount(nint array);

    [LibraryImport(Library)]
    public static partial nint CFArrayGetValueAtIndex(nint array, long index);

    [LibraryImport(Library)]
    public static partial nint CFDictionaryGetValue(nint dictionary, nint key);

    [LibraryImport(Library)]
    public static partial nint CFDictionaryCreate(nint allocator, ref nint keys, ref nint values, long count, nint keyCallBacks, nint valueCallBacks);

    [LibraryImport(Library)]
    public static partial nint CFRunLoopGetMain();

    [LibraryImport(Library)]
    public static partial void CFRunLoopAddSource(nint runLoop, nint source, nint mode);

    [LibraryImport(Library)]
    public static partial void CFRunLoopRemoveSource(nint runLoop, nint source, nint mode);

    [LibraryImport(Library)]
    public static partial nint CFMachPortCreateRunLoopSource(nint allocator, nint port, long order);

    [LibraryImport(Library)]
    public static partial void CFMachPortInvalidate(nint port);
}
