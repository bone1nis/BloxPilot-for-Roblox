using System.Text.RegularExpressions;

namespace RobloxAntiAfk.Core.Domain;

/// <summary>Превращает ссылку на игру в адрес запуска. В адрес попадают только проверенные цифры и код сервера.</summary>
public static partial class GameLink
{
    public static string? ToLaunchUri(string? text)
    {
        var value = text?.Trim() ?? "";
        var place = PlaceId().Match(value);
        if (!place.Success)
        {
            return null;
        }

        var id = place.Groups["id"].Value;
        var code = LinkCode().Match(value);
        return code.Success
            ? $"roblox://experiences/start?placeId={id}&linkCode={code.Groups["code"].Value}"
            : $"roblox://experiences/start?placeId={id}";
    }

    [GeneratedRegex(@"^(?<id>\d{1,20})$|roblox\.com/(?:[a-z]{2}(?:-[a-z]{2})?/)?games/(?<id>\d{1,20})|[?&]placeId=(?<id>\d{1,20})|roblox://placeId=(?<id>\d{1,20})", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceId();

    [GeneratedRegex(@"[?&](?:privateServerLinkCode|linkCode)=(?<code>[A-Za-z0-9_-]{1,100})")]
    private static partial Regex LinkCode();
}
