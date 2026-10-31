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
        [GameKey.B] = 0x30,
        [GameKey.C] = 0x2E,
        [GameKey.G] = 0x22,
        [GameKey.H] = 0x23,
        [GameKey.J] = 0x24,
        [GameKey.K] = 0x25,
        [GameKey.L] = 0x26,
        [GameKey.M] = 0x32,
        [GameKey.N] = 0x31,
        [GameKey.P] = 0x19,
        [GameKey.T] = 0x14,
        [GameKey.U] = 0x16,
        [GameKey.V] = 0x2F,
        [GameKey.X] = 0x2D,
        [GameKey.Y] = 0x15,
        [GameKey.Z] = 0x2C,
        [GameKey.Four] = 0x05,
        [GameKey.Five] = 0x06,
        [GameKey.Six] = 0x07,
        [GameKey.Seven] = 0x08,
        [GameKey.Eight] = 0x09,
        [GameKey.Nine] = 0x0A,
        [GameKey.Zero] = 0x0B,
        [GameKey.Escape] = 0x01,
        [GameKey.Enter] = 0x1C,
        [GameKey.Tab] = 0x0F,
        [GameKey.Backspace] = 0x0E,
        [GameKey.Shift] = 0x2A,
        [GameKey.Control] = 0x1D,
        [GameKey.Up] = 0x48,
        [GameKey.Down] = 0x50,
        [GameKey.Left] = 0x4B,
        [GameKey.Right] = 0x4D,
    };

    /// <summary>У стрелок такие же коды, как у цифрового блока, отличает их только признак расширенной клавиши.</summary>
    private static readonly HashSet<GameKey> ExtendedKeys = [GameKey.Up, GameKey.Down, GameKey.Left, GameKey.Right];

    private static readonly Dictionary<ushort, GameKey> KeysByScanCode = ScanCodes.ToDictionary(pair => pair.Value, pair => pair.Key);

    internal static bool TryGetKey(ushort scanCode, bool isExtended, out GameKey key) =>
        KeysByScanCode.TryGetValue(scanCode, out key) && (isExtended || !ExtendedKeys.Contains(key));

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
                    Flags = NativeMethods.KeyEventScanCode
                        | (isDown ? 0 : NativeMethods.KeyEventUp)
                        | (ExtendedKeys.Contains(key) ? NativeMethods.KeyEventExtended : 0),
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
