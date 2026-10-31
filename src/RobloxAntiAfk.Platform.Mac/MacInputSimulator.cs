using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Platform.Mac.Native;

namespace RobloxAntiAfk.Platform.Mac;

internal sealed class MacInputSimulator : IInputSimulator
{
    /// <summary>Физические коды клавиш: нажатие не зависит от раскладки.</summary>
    internal static readonly IReadOnlyDictionary<GameKey, ushort> KeyCodes = new Dictionary<GameKey, ushort>
    {
        [GameKey.Space] = 49,
        [GameKey.W] = 13,
        [GameKey.A] = 0,
        [GameKey.S] = 1,
        [GameKey.D] = 2,
        [GameKey.E] = 14,
        [GameKey.F] = 3,
        [GameKey.Q] = 12,
        [GameKey.R] = 15,
        [GameKey.One] = 18,
        [GameKey.Two] = 19,
        [GameKey.Three] = 20,
        [GameKey.I] = 34,
        [GameKey.O] = 31,
        [GameKey.B] = 11,
        [GameKey.C] = 8,
        [GameKey.G] = 5,
        [GameKey.H] = 4,
        [GameKey.J] = 38,
        [GameKey.K] = 40,
        [GameKey.L] = 37,
        [GameKey.M] = 46,
        [GameKey.N] = 45,
        [GameKey.P] = 35,
        [GameKey.T] = 17,
        [GameKey.U] = 32,
        [GameKey.V] = 9,
        [GameKey.X] = 7,
        [GameKey.Y] = 16,
        [GameKey.Z] = 6,
        [GameKey.Four] = 21,
        [GameKey.Five] = 23,
        [GameKey.Six] = 22,
        [GameKey.Seven] = 26,
        [GameKey.Eight] = 28,
        [GameKey.Nine] = 25,
        [GameKey.Zero] = 29,
        [GameKey.Escape] = 53,
        [GameKey.Enter] = 36,
        [GameKey.Tab] = 48,
        [GameKey.Backspace] = 51,
        [GameKey.Shift] = 56,
        [GameKey.Control] = 59,
        [GameKey.Up] = 126,
        [GameKey.Down] = 125,
        [GameKey.Left] = 123,
        [GameKey.Right] = 124,
    };

    public ScreenPoint GetCursor()
    {
        var cgEvent = CoreGraphics.CGEventCreate(0);
        var location = CoreGraphics.CGEventGetLocation(cgEvent);
        CoreFoundation.CFRelease(cgEvent);
        return new ScreenPoint(location.X, location.Y);
    }

    public void MoveCursor(ScreenPoint point) =>
        PostMouse(CoreGraphics.EventMouseMoved, point, CoreGraphics.ButtonLeft);

    public void SendMouseButton(MouseButton button, bool isDown, ScreenPoint at)
    {
        var eventType = (button, isDown) switch
        {
            (MouseButton.Left, true) => CoreGraphics.EventLeftMouseDown,
            (MouseButton.Left, false) => CoreGraphics.EventLeftMouseUp,
            (MouseButton.Right, true) => CoreGraphics.EventRightMouseDown,
            _ => CoreGraphics.EventRightMouseUp,
        };
        PostMouse(eventType, at, button == MouseButton.Left ? CoreGraphics.ButtonLeft : CoreGraphics.ButtonRight);
    }

    public void SendKey(GameKey key, bool isDown)
    {
        var cgEvent = CoreGraphics.CGEventCreateKeyboardEvent(0, KeyCodes[key], isDown ? (byte)1 : (byte)0);
        CoreGraphics.CGEventPost(CoreGraphics.HidEventTap, cgEvent);
        CoreFoundation.CFRelease(cgEvent);
    }

    private static void PostMouse(uint eventType, ScreenPoint point, int button)
    {
        var position = new CGPoint { X = point.X, Y = point.Y };
        var cgEvent = CoreGraphics.CGEventCreateMouseEvent(0, eventType, position, button);
        CoreGraphics.CGEventPost(CoreGraphics.HidEventTap, cgEvent);
        CoreFoundation.CFRelease(cgEvent);
    }
}
