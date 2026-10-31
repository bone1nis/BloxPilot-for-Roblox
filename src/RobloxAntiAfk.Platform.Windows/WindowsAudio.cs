using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;

namespace RobloxAntiAfk.Platform.Windows;

/// <summary>Выключает звук Roblox в системном микшере, как если бы это сделали вручную. Сам клиент не затрагивается.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsAudio : IRobloxAudio
{
    private const int RenderFlow = 0;
    private const int MultimediaRole = 1;
    private const uint AllContexts = 23;

    public bool IsSupported => true;

    public void SetMuted(IReadOnlyList<WindowHandle> windows, bool isMuted)
    {
        var processes = new HashSet<uint>();
        foreach (var window in windows)
        {
            NativeMethods.GetWindowThreadProcessId(window.Value, out var processId);
            processes.Add(processId);
        }

        if (processes.Count == 0)
        {
            return;
        }

        try
        {
            var enumerator = (IDeviceEnumerator)new DeviceEnumerator();
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(RenderFlow, MultimediaRole, out var device));
            var managerId = typeof(ISessionManager).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(ref managerId, AllContexts, 0, out var managerObject));
            var manager = (ISessionManager)managerObject;
            Marshal.ThrowExceptionForHR(manager.GetSessionEnumerator(out var sessions));
            Marshal.ThrowExceptionForHR(sessions.GetCount(out var count));

            var noContext = Guid.Empty;
            for (var index = 0; index < count; index++)
            {
                if (sessions.GetSession(index, out var session) == 0
                    && ((ISessionControl)session).GetProcessId(out var processId) == 0
                    && processes.Contains(processId))
                {
                    ((ISimpleVolume)session).SetMute(isMuted ? 1 : 0, ref noContext);
                }
            }
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException)
        {
            // Нет устройства вывода или служба звука недоступна: звук не главное, работаем дальше.
        }
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class DeviceEnumerator;

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints();

        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDevice
    {
        [PreserveSig] int Activate(ref Guid interfaceId, uint context, nint parameters, [MarshalAs(UnmanagedType.IUnknown)] out object result);
    }

    [ComImport]
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISessionManager
    {
        [PreserveSig] int GetAudioSessionControl();

        [PreserveSig] int GetSimpleAudioVolume();

        [PreserveSig] int GetSessionEnumerator(out ISessionEnumerator sessions);
    }

    [ComImport]
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);

        [PreserveSig] int GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object session);
    }

    /// <summary>Первые девять методов принадлежат базовому интерфейсу и здесь не вызываются.</summary>
    [ComImport]
    [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISessionControl
    {
        [PreserveSig] int GetState();

        [PreserveSig] int GetDisplayName();

        [PreserveSig] int SetDisplayName();

        [PreserveSig] int GetIconPath();

        [PreserveSig] int SetIconPath();

        [PreserveSig] int GetGroupingParam();

        [PreserveSig] int SetGroupingParam();

        [PreserveSig] int RegisterAudioSessionNotification();

        [PreserveSig] int UnregisterAudioSessionNotification();

        [PreserveSig] int GetSessionIdentifier();

        [PreserveSig] int GetSessionInstanceIdentifier();

        [PreserveSig] int GetProcessId(out uint processId);
    }

    [ComImport]
    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleVolume
    {
        [PreserveSig] int SetMasterVolume();

        [PreserveSig] int GetMasterVolume();

        [PreserveSig] int SetMute(int isMuted, ref Guid eventContext);
    }
}
