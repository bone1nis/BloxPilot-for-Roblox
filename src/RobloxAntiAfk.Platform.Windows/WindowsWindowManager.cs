using System.Diagnostics;
using System.Runtime.InteropServices;
using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;

namespace RobloxAntiAfk.Platform.Windows;

internal sealed unsafe class WindowsWindowManager(WindowsInputSimulator input) : IWindowManager
{
    private const string RobloxTitle = "Roblox";

    /// <summary>Вызывается только из потока интерфейса, поэтому статический буфер безопасен.</summary>
    private static readonly List<WindowHandle> Found = [];

    public IReadOnlyList<WindowHandle> FindRobloxWindows()
    {
        Found.Clear();
        NativeMethods.EnumWindows(&CollectRobloxWindow, 0);
        return Found.ToArray();
    }

    public WindowHandle GetForegroundWindow() => new(NativeMethods.GetForegroundWindow());

    /// <summary>
    /// Windows разрешает менять активное окно только процессу, получившему последний ввод,
    /// поэтому сначала отправляем пустое событие мыши.
    /// </summary>
    public void RequestForeground(WindowHandle window)
    {
        input.SendEmptyMouseEvent();
        NativeMethods.SetForegroundWindow(window.Value);
    }

    public ScreenRect? GetBounds(WindowHandle window)
    {
        if (NativeMethods.GetWindowRect(window.Value, out var rect) == 0)
        {
            return null;
        }

        return new ScreenRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    public bool IsMinimized(WindowHandle window) => NativeMethods.IsIconic(window.Value) != 0;

    public void Restore(WindowHandle window) => NativeMethods.ShowWindow(window.Value, NativeMethods.ShowRestored);

    public void Minimize(WindowHandle window) => NativeMethods.ShowWindow(window.Value, NativeMethods.ShowMinimized);

    /// <summary>
    /// Ищем по точному заголовку: вкладка браузера называется иначе,
    /// а открытую папку с таким именем отсекаем по процессу проводника.
    /// </summary>
    [UnmanagedCallersOnly]
    private static int CollectRobloxWindow(nint window, nint parameter)
    {
        if (NativeMethods.IsWindowVisible(window) == 0)
        {
            return 1;
        }

        char* buffer = stackalloc char[64];
        var length = NativeMethods.GetWindowText(window, buffer, 64);
        if (new ReadOnlySpan<char>(buffer, length).SequenceEqual(RobloxTitle) && !IsExplorer(window))
        {
            Found.Add(new WindowHandle(window));
        }

        return 1;
    }

    private static bool IsExplorer(nint window)
    {
        NativeMethods.GetWindowThreadProcessId(window, out var processId);
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }
}
