using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RobloxAntiAfk.Core.Services;

public enum TelegramLookupStatus
{
    Found,

    /// <summary>Токен подходит, но новых сообщений боту нет.</summary>
    NoMessages,

    TokenRejected,

    /// <summary>Сообщения бота уже забирает другая программа или сервис.</summary>
    Conflict,

    /// <summary>До Telegram не удалось достучаться, либо ответил кто-то другой.</summary>
    Unreachable,
}

public sealed record TelegramLookup(TelegramLookupStatus Status, string? ChatId = null, string? BotName = null);

/// <summary>Пишет в Telegram через бота пользователя. Адрес с токеном никуда не выводится, поэтому ошибки только считаются неудачей.</summary>
public sealed partial class TelegramNotifier(HttpClient http)
{
    private const string DirectApiRoot = "https://api.telegram.org/bot";
    private static readonly TimeSpan ConnectAttemptTimeout = TimeSpan.FromSeconds(5);
    private static readonly string[] UpdatesWithChat = ["message", "edited_message", "my_chat_member"];

    private string _apiRoot = DirectApiRoot;

    public bool IsUsingRelay => _apiRoot != DirectApiRoot;

    /// <summary>В адресе запроса есть токен бота, поэтому посредник подходит только по https.</summary>
    public static bool IsValidRelayUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.Query.Length == 0
        && uri.Fragment.Length == 0;

    /// <summary>Посредник нужен, если api.telegram.org закрыт у провайдера. Без подходящего адреса запросы идут напрямую.</summary>
    public void UseRelay(string? relayUrl) =>
        _apiRoot = IsValidRelayUrl(relayUrl) ? $"{relayUrl!.TrimEnd('/')}/bot" : DirectApiRoot;

    /// <summary>
    /// .NET пробует адреса сервера по очереди. Если IPv6 в сети молча не работает, до IPv4 он доходит слишком поздно,
    /// поэтому первым идёт IPv4, а на каждый адрес отведено ограниченное время.
    /// </summary>
    public static HttpClient CreateHttpClient(TimeSpan timeout) =>
        new(new SocketsHttpHandler { ConnectCallback = ConnectAsync }) { Timeout = timeout };

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellation)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellation);
        Exception? lastError = null;
        foreach (var address in addresses.OrderBy(address => address.AddressFamily == AddressFamily.InterNetwork ? 0 : 1))
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            attempt.CancelAfter(ConnectAttemptTimeout);
            try
            {
                await socket.ConnectAsync(address, context.DnsEndPoint.Port, attempt.Token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception exception) when (!cancellation.IsCancellationRequested && exception is SocketException or OperationCanceledException)
            {
                socket.Dispose();
                lastError = exception;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        throw new HttpRequestException("Нет связи с сервером.", lastError);
    }

    public static bool IsValidToken(string? token) => token is not null && TokenFormat().IsMatch(token);

    public async Task<bool> SendAsync(string token, string chatId, string text)
    {
        if (!IsValidToken(token) || string.IsNullOrEmpty(chatId))
        {
            return false;
        }

        try
        {
            using var response = await http.PostAsJsonAsync($"{_apiRoot}{token}/sendMessage", new SendMessageRequest(chatId, text));
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>Находит чат по последнему сообщению, которое пользователь написал своему боту, а если не нашёл, объясняет почему.</summary>
    public async Task<TelegramLookup> FindChatAsync(string token)
    {
        if (!IsValidToken(token))
        {
            return new TelegramLookup(TelegramLookupStatus.TokenRejected);
        }

        try
        {
            using var response = await http.GetAsync($"{_apiRoot}{token}/getUpdates");
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
            {
                return new TelegramLookup(TelegramLookupStatus.TokenRejected);
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                return new TelegramLookup(TelegramLookupStatus.Conflict);
            }

            if (!response.IsSuccessStatusCode)
            {
                return new TelegramLookup(TelegramLookupStatus.Unreachable);
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return LastChatId(document) is { } chatId
                ? new TelegramLookup(TelegramLookupStatus.Found, chatId)
                : new TelegramLookup(TelegramLookupStatus.NoMessages, BotName: await FindBotNameAsync(token));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return new TelegramLookup(TelegramLookupStatus.Unreachable);
        }
    }

    private static string? LastChatId(JsonDocument document)
    {
        string? chatId = null;
        foreach (var update in document.RootElement.GetProperty("result").EnumerateArray())
        {
            foreach (var kind in UpdatesWithChat)
            {
                if (update.TryGetProperty(kind, out var content)
                    && content.TryGetProperty("chat", out var chat)
                    && chat.TryGetProperty("id", out var id))
                {
                    chatId = id.GetRawText();
                }
            }
        }

        return chatId;
    }

    /// <summary>Имя бота нужно, чтобы пользователь сверил, тому ли боту написал.</summary>
    private async Task<string?> FindBotNameAsync(string token)
    {
        using var response = await http.GetAsync($"{_apiRoot}{token}/getMe");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("result").TryGetProperty("username", out var name) ? name.GetString() : null;
    }

    private sealed record SendMessageRequest(
        [property: System.Text.Json.Serialization.JsonPropertyName("chat_id")] string ChatId,
        [property: System.Text.Json.Serialization.JsonPropertyName("text")] string Text);

    [GeneratedRegex(@"^\d{1,20}:[A-Za-z0-9_-]{20,100}$")]
    private static partial Regex TokenFormat();
}
