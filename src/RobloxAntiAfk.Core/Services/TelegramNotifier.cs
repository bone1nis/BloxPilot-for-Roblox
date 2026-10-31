using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RobloxAntiAfk.Core.Services;

/// <summary>Пишет в Telegram через бота пользователя. Адрес с токеном никуда не выводится, поэтому ошибки только считаются неудачей.</summary>
public sealed partial class TelegramNotifier(HttpClient http)
{
    private const string ApiRoot = "https://api.telegram.org/bot";

    public static bool IsValidToken(string? token) => token is not null && TokenFormat().IsMatch(token);

    public async Task<bool> SendAsync(string token, string chatId, string text)
    {
        if (!IsValidToken(token) || string.IsNullOrEmpty(chatId))
        {
            return false;
        }

        try
        {
            using var response = await http.PostAsJsonAsync($"{ApiRoot}{token}/sendMessage", new SendMessageRequest(chatId, text));
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>Находит чат по последнему сообщению, которое пользователь написал своему боту.</summary>
    public async Task<string?> FindChatIdAsync(string token)
    {
        if (!IsValidToken(token))
        {
            return null;
        }

        try
        {
            using var response = await http.GetAsync($"{ApiRoot}{token}/getUpdates");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            string? chatId = null;
            foreach (var update in document.RootElement.GetProperty("result").EnumerateArray())
            {
                if (update.TryGetProperty("message", out var message)
                    && message.TryGetProperty("chat", out var chat)
                    && chat.TryGetProperty("id", out var id))
                {
                    chatId = id.GetRawText();
                }
            }

            return chatId;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    private sealed record SendMessageRequest(
        [property: System.Text.Json.Serialization.JsonPropertyName("chat_id")] string ChatId,
        [property: System.Text.Json.Serialization.JsonPropertyName("text")] string Text);

    [GeneratedRegex(@"^\d{1,20}:[A-Za-z0-9_-]{20,100}$")]
    private static partial Regex TokenFormat();
}
