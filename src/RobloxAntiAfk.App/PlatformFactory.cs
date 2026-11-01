using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Platform.Mac;
using RobloxAntiAfk.Platform.Windows;

namespace RobloxAntiAfk.App;

internal static class PlatformFactory
{
    public static PlatformServices Create()
    {
        if (OperatingSystem.IsWindows())
        {
#if WINDOWS_OCR
            return WindowsPlatform.Create(new RobloxAntiAfk.Ocr.RapidTextRecognizer());
#else
            return WindowsPlatform.Create();
#endif
        }

        if (OperatingSystem.IsMacOS())
        {
            return MacPlatform.Create();
        }

        throw new PlatformNotSupportedException("BloxPilot работает только на Windows и macOS.");
    }
}
