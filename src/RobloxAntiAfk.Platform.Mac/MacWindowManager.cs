using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Platform.Mac.Native;

namespace RobloxAntiAfk.Platform.Mac;

/// <summary>На Mac окном считается приложение целиком, поэтому <see cref="WindowHandle"/> здесь хранит PID.</summary>
internal sealed class MacWindowManager : IWindowManager
{
    private const string RobloxBundleId = "com.roblox.RobloxPlayer";

    private static readonly nint OwnerPidKey = CoreFoundation.CreateString("kCGWindowOwnerPID");
    private static readonly nint LayerKey = CoreFoundation.CreateString("kCGWindowLayer");
    private static readonly nint BoundsKey = CoreFoundation.CreateString("kCGWindowBounds");
    private static readonly nint FrontmostAttribute = CoreFoundation.CreateString("AXFrontmost");
    private static readonly nint RobloxBundleIdString = CoreFoundation.CreateString(RobloxBundleId);

    public IReadOnlyList<WindowHandle> FindRobloxWindows()
    {
        var pool = ObjC.objc_autoreleasePoolPush();
        try
        {
            var apps = ObjC.Send(ObjC.Class("NSRunningApplication"), "runningApplicationsWithBundleIdentifier:", RobloxBundleIdString);
            var count = ObjC.SendLong(apps, "count");
            var result = new WindowHandle[count];
            for (var index = 0L; index < count; index++)
            {
                result[index] = new WindowHandle(ObjC.SendInt(ObjC.SendIndex(apps, "objectAtIndex:", index), "processIdentifier"));
            }

            return result;
        }
        finally
        {
            ObjC.objc_autoreleasePoolPop(pool);
        }
    }

    public WindowHandle GetForegroundWindow()
    {
        var pool = ObjC.objc_autoreleasePoolPush();
        try
        {
            var workspace = ObjC.Send(ObjC.Class("NSWorkspace"), "sharedWorkspace");
            var front = ObjC.Send(workspace, "frontmostApplication");
            return front == 0 ? WindowHandle.None : new WindowHandle(ObjC.SendInt(front, "processIdentifier"));
        }
        finally
        {
            ObjC.objc_autoreleasePoolPop(pool);
        }
    }

    public void RequestForeground(WindowHandle window)
    {
        var element = Accessibility.AXUIElementCreateApplication((int)window.Value);
        Accessibility.AXUIElementSetAttributeValue(element, FrontmostAttribute, CoreFoundation.BooleanTrue);
        CoreFoundation.CFRelease(element);
    }

    /// <summary>Самое большое видимое окно приложения: у Roblox бывают мелкие служебные окна.</summary>
    public ScreenRect? GetBounds(WindowHandle window)
    {
        var list = CoreGraphics.CGWindowListCopyWindowInfo(
            CoreGraphics.WindowListOnScreenOnly | CoreGraphics.WindowListExcludeDesktopElements, 0);
        if (list == 0)
        {
            return null;
        }

        try
        {
            ScreenRect? largest = null;
            var count = CoreFoundation.CFArrayGetCount(list);
            for (var index = 0L; index < count; index++)
            {
                var info = CoreFoundation.CFArrayGetValueAtIndex(list, index);
                if (CoreFoundation.GetNumber(CoreFoundation.CFDictionaryGetValue(info, OwnerPidKey)) != window.Value
                    || CoreFoundation.GetNumber(CoreFoundation.CFDictionaryGetValue(info, LayerKey)) != 0
                    || CoreGraphics.CGRectMakeWithDictionaryRepresentation(CoreFoundation.CFDictionaryGetValue(info, BoundsKey), out var rect) == 0)
                {
                    continue;
                }

                if (largest is null || rect.Width * rect.Height > largest.Value.Width * largest.Value.Height)
                {
                    largest = new ScreenRect(rect.X, rect.Y, rect.Width, rect.Height);
                }
            }

            return largest;
        }
        finally
        {
            CoreFoundation.CFRelease(list);
        }
    }

    /// <summary>
    /// На Mac окном считается приложение, поэтому здесь это «скрыть приложение», как Cmd+H.
    /// Окно, свёрнутое в Dock, не имеет границ на экране, движок сообщит об этом пользователю.
    /// </summary>
    public bool IsMinimized(WindowHandle window) => WithApp(window, app => ObjC.SendBool(app, "isHidden"));

    public void Restore(WindowHandle window) => WithApp(window, app => ObjC.Send(app, "unhide") != 0);

    public void Minimize(WindowHandle window) => WithApp(window, app => ObjC.Send(app, "hide") != 0);

    private static bool WithApp(WindowHandle window, Func<nint, bool> use)
    {
        var pool = ObjC.objc_autoreleasePoolPush();
        try
        {
            var app = ObjC.Send(ObjC.Class("NSRunningApplication"), "runningApplicationWithProcessIdentifier:", window.Value);
            return app != 0 && use(app);
        }
        finally
        {
            ObjC.objc_autoreleasePoolPop(pool);
        }
    }
}
