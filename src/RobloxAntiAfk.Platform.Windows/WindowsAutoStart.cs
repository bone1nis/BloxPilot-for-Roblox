using System.Runtime.Versioning;
using Microsoft.Win32;
using RobloxAntiAfk.Core.Abstractions;

namespace RobloxAntiAfk.Platform.Windows;

[SupportedOSPlatform("windows")]
internal sealed class WindowsAutoStart : IAutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Roblox AntiAFK";

    public void Apply(bool isEnabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (isEnabled && Environment.ProcessPath is { } path)
        {
            key.SetValue(ValueName, $"\"{path}\" --autostart");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
