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
