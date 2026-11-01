using System.Text.Json.Serialization;

namespace RobloxAntiAfk.Core.Domain;

public sealed record AppSettings
{
    public const int MinIntervalMinutes = 1;

    /// <summary>Roblox обычно кикает примерно через 20 минут, поэтому интервал должен быть меньше.</summary>
    public const int MaxIntervalMinutes = 19;

    public static readonly IReadOnlyList<int> ResetChoices = [0, 15, 30, 60];

    public int IntervalMinutes { get; init; } = 5;
    public AfkAction Action { get; init; } = AfkAction.RightClick;
    public GameKey Key { get; init; } = GameKey.Space;
    public WindowPoint? ClickPoint { get; init; }
    public bool AllWindows { get; init; } = true;
    public bool SkipWhilePlaying { get; init; } = true;
    public bool KeepAwake { get; init; } = true;

    /// <summary>0 значит, что сброс выключен.</summary>
    public int ResetMinutes { get; init; }
    public Macro? Macro { get; init; }
    public bool HideBetweenActions { get; init; }
    public bool MuteRoblox { get; init; }
    public bool CloseToTray { get; init; }
    public bool StartWithSystem { get; init; }
    public bool RunOnOpen { get; init; }
    public bool ReconnectEnabled { get; init; }
    public string GameLink { get; init; } = "";
    public string TelegramToken { get; init; } = "";
    public string TelegramChatId { get; init; } = "";
    public string TelegramRelayUrl { get; init; } = "";
    public bool TranslationEnabled { get; init; }
    public WindowArea? TranslationArea { get; init; }
    public bool TranslationAutoRefresh { get; init; }

    /// <summary>Сочетание каждый раз предлагает обвести текст заново. Если выключить, читается запомненная область.</summary>
    public bool TranslationSelectEachTime { get; init; } = true;
    public string DeepLKey { get; init; } = "";

    /// <summary>null значит стандартное сочетание этой системы.</summary>
    public Hotkey? ToggleHotkey { get; init; }
    public Hotkey? TranslationHotkey { get; init; }

    [JsonIgnore]
    public TimeSpan Interval => TimeSpan.FromMinutes(IntervalMinutes);

    [JsonIgnore]
    public bool UsesMouse => Action is AfkAction.RightClick or AfkAction.LeftClick;

    public AppSettings Normalized() => this with
    {
        IntervalMinutes = Math.Clamp(IntervalMinutes, MinIntervalMinutes, MaxIntervalMinutes),
        ClickPoint = ClickPoint?.Clamped(),
        ResetMinutes = ResetChoices.Contains(ResetMinutes) ? ResetMinutes : 0,
        GameLink = GameLink ?? "",
        TelegramToken = (TelegramToken ?? "").Trim(),
        TelegramChatId = TelegramChatId ?? "",
        TelegramRelayUrl = (TelegramRelayUrl ?? "").Trim(),
        TranslationArea = TranslationArea?.Clamped(),
        DeepLKey = (DeepLKey ?? "").Trim(),
    };

    /// <summary>Сброс настроек не должен стирать то, что нельзя вернуть одной кнопкой.</summary>
    public AppSettings ToDefaultsKeepingUserData() => new()
    {
        Macro = Macro,
        GameLink = GameLink,
        TelegramToken = TelegramToken,
        TelegramChatId = TelegramChatId,
        TelegramRelayUrl = TelegramRelayUrl,
        TranslationArea = TranslationArea,
        DeepLKey = DeepLKey,
    };
}
