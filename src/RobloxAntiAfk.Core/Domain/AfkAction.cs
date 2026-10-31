namespace RobloxAntiAfk.Core.Domain;

public enum AfkAction
{
    RightClick,
    LeftClick,
    Key,
    Walk,
    Zoom,
    Macro,
}

/// <summary>Новые клавиши добавляются в конец: настройки хранят названия, но порядок удобнее не менять.</summary>
public enum GameKey
{
    Space,
    W,
    A,
    S,
    D,
    E,
    F,
    Q,
    R,
    One,
    Two,
    Three,
    I,
    O,
    B,
    C,
    G,
    H,
    J,
    K,
    L,
    M,
    N,
    P,
    T,
    U,
    V,
    X,
    Y,
    Z,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Zero,
    Escape,
    Enter,
    Tab,
    Backspace,
    Shift,
    Control,
    Up,
    Down,
    Left,
    Right,
}

public enum MouseButton
{
    Left,
    Right,
}
