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
            return WindowsPlatform.Create();
        }

        if (OperatingSystem.IsMacOS())
        {
            return MacPlatform.Create();
        }

        throw new PlatformNotSupportedException("Roblox AntiAFK работает только на Windows и macOS.");
    }
}
