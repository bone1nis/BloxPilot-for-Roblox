using System.Runtime.InteropServices;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Platform.Mac;
using RobloxAntiAfk.Platform.Windows;

namespace RobloxAntiAfk.Core.Tests;

/// <summary>Эти проверки не вызывают систему: они сверяют таблицы и размеры структур на любой ОС.</summary>
public class PlatformMappingTests
{
    public static TheoryData<GameKey> AllKeys => new(Enum.GetValues<GameKey>());

    [Theory]
    [MemberData(nameof(AllKeys))]
    public void Every_key_has_a_windows_scan_code(GameKey key) =>
        Assert.True(WindowsInputSimulator.ScanCodes.ContainsKey(key));

    [Theory]
    [MemberData(nameof(AllKeys))]
    public void Every_key_has_a_mac_key_code(GameKey key) =>
        Assert.True(MacInputSimulator.KeyCodes.ContainsKey(key));

    [Fact]
    public void Scan_codes_are_unique() =>
        Assert.Equal(WindowsInputSimulator.ScanCodes.Count, WindowsInputSimulator.ScanCodes.Values.Distinct().Count());

    [Fact]
    public void Mac_key_codes_are_unique() =>
        Assert.Equal(MacInputSimulator.KeyCodes.Count, MacInputSimulator.KeyCodes.Values.Distinct().Count());

    /// <summary>SendInput молча ничего не делает, если размер структуры INPUT неверный. На x64 он равен 40 байтам.</summary>
    [Fact]
    public void Windows_input_struct_matches_native_layout()
    {
        Assert.Equal(40, Marshal.SizeOf<Input>());
        Assert.Equal(8, (int)Marshal.OffsetOf<Input>(nameof(Input.Data)));
    }

    /// <summary>Эти структуры заполняет или читает система: на x64 их размеры заданы её заголовками.</summary>
    [Fact]
    public void Windows_hook_and_class_structs_match_native_layout()
    {
        Assert.Equal(24, Marshal.SizeOf<KeyboardHookData>());
        Assert.Equal(32, Marshal.SizeOf<MouseHookData>());
        Assert.Equal(80, Marshal.SizeOf<WindowClass>());
    }

    [Fact]
    public void Arrow_keys_are_told_apart_from_numeric_keypad()
    {
        var up = WindowsInputSimulator.ScanCodes[GameKey.Up];

        Assert.True(WindowsInputSimulator.TryGetKey(up, isExtended: true, out var key));
        Assert.Equal(GameKey.Up, key);
        Assert.False(WindowsInputSimulator.TryGetKey(up, isExtended: false, out _));
    }

    [Fact]
    public void Mac_autostart_agent_escapes_the_application_path()
    {
        var agent = MacAutoStart.BuildAgent("/Applications/A&B/Roblox AntiAFK");

        Assert.Contains("<string>/Applications/A&amp;B/Roblox AntiAFK</string>", agent);
        Assert.Contains("<string>--autostart</string>", agent);
    }
}
