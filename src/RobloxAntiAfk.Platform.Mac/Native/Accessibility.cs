using System.Runtime.InteropServices;

namespace RobloxAntiAfk.Platform.Mac.Native;

internal static partial class Accessibility
{
    private const string Library = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

    [LibraryImport(Library)]
    public static partial byte AXIsProcessTrusted();

    [LibraryImport(Library)]
    public static partial byte AXIsProcessTrustedWithOptions(nint options);

    [LibraryImport(Library)]
    public static partial nint AXUIElementCreateApplication(int processId);

    [LibraryImport(Library)]
    public static partial int AXUIElementSetAttributeValue(nint element, nint attribute, nint value);
}
