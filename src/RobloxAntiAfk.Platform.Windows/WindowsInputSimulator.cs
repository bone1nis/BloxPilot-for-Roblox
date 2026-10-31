using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;

namespace RobloxAntiAfk.Platform.Windows;

internal sealed unsafe class WindowsInputSimulator : IInputSimulator
{
    /// <summary>Скан-коды физических клавиш: нажатие не зависит от раскладки.</summary>
    internal static readonly IReadOnlyDictionary<GameKey, ushort> ScanCodes = new Dictionary<GameKey, ushort>
    {
        [GameKey.Space] = 0x39,
        [GameKey.W] = 0x11,
        [GameKey.A] = 0x1E,
        [GameKey.S] = 0x1F,
        [GameKey.D] = 0x20,
        [GameKey.E] = 0x12,
        [GameKey.F] = 0x21,
        [GameKey.Q] = 0x10,
        [GameKey.R] = 0x13,
        [GameKey.One] = 0x02,
        [GameKey.Two] = 0x03,
        [GameKey.Three] = 0x04,
        [GameKey.I] = 0x17,
        [GameKey.O] = 0x18,
    };

    public ScreenPoint GetCursor()
    {
        NativeMethods.GetCursorPos(out var point);
        return new ScreenPoint(point.X, point.Y);
    }

    public void MoveCursor(ScreenPoint point) => NativeMethods.SetCursorPos((int)point.X, (int)point.Y);

    public void SendMouseButton(MouseButton button, bool isDown, ScreenPoint at)
    {
        var flags = (button, isDown) switch
        {
            (MouseButton.Left, true) => NativeMethods.MouseLeftDown,
            (MouseButton.Left, false) => NativeMethods.MouseLeftUp,
            (MouseButton.Right, true) => NativeMethods.MouseRightDown,
            _ => NativeMethods.MouseRightUp,
        };
        SendMouse(flags);
    }

    public void SendKey(GameKey key, bool isDown)
    {
        var input = new Input
        {
            Type = NativeMethods.InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    ScanCode = ScanCodes[key],
                    Flags = NativeMethods.KeyEventScanCode | (isDown ? 0 : NativeMethods.KeyEventUp),
                },
            },
        };
        NativeMethods.SendInput(1, &input, sizeof(Input));
    }

    public void SendEmptyMouseEvent() => SendMouse(0);

    private static void SendMouse(uint flags)
    {
        var input = new Input
        {
            Type = NativeMethods.InputMouse,
            Data = new InputUnion { Mouse = new MouseInput { Flags = flags } },
        };
        NativeMethods.SendInput(1, &input, sizeof(Input));
    }
}
