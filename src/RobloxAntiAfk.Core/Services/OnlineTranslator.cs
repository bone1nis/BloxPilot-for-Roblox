using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RobloxAntiAfk.Core.Services;

/// <summary>
/// Переводит на русский через интернет. В сервис уходит только распознанный текст, снимки экрана остаются на компьютере.
/// Сначала DeepL, если указан ключ, затем бесплатные сервисы: если один не ответил, пробуем следующий.
/// </summary>
public sealed class OnlineTranslator(HttpClient http)
{
    private const string GoogleUrl = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl=ru&dt=t";
    private const string MyMemoryUrl = "https://api.mymemory.translated.net/get?langpair=en%7Cru&q=";
    private const string DeepLPaidUrl = "https://api.deepl.com/v2/translate";
    private const string DeepLFreeUrl = "https://api-free.deepl.com/v2/translate";
    private const string DeepLFreeKeySuffix = ":fx";
    private const int MaxLength = 2000;
    private const int MaxMyMemoryLength = 450;
    private const int MaxCacheSize = 200;

    private readonly Dictionary<string, string> _cache = [];

    /// <summary>Возвращает перевод или null, если ни один сервис не ответил. Вызывается из потока интерфейса.</summary>
    public async Task<string?> TranslateAsync(string text, string deepLKey, CancellationToken cancellation)
    {
        var source = text.Length > MaxLength ? text[..MaxLength] : text;
        if (_cache.TryGetValue(source, out var cached))
        {
            return cached;
        }

        var result = await TryDeepLAsync(source, deepLKey, cancellation)
            ?? await TryGoogleAsync(source, cancellation)
            ?? await TryMyMemoryAsync(source, cancellation);
        if (result is null)
        {
            return null;
        }

        if (_cache.Count >= MaxCacheSize)
        {
            _cache.Clear();
        }

        _cache[source] = result;
        return result;
    }

    private async Task<string?> TryDeepLAsync(string text, string key, CancellationToken cancellation)
    {
        if (key.Length == 0)
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, key.EndsWith(DeepLFreeKeySuffix, StringComparison.Ordinal) ? DeepLFreeUrl : DeepLPaidUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["text"] = text, ["target_lang"] = "RU" }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", key);
        return await SendAsync(request, ParseDeepL, cancellation);
    }

    private async Task<string?> TryGoogleAsync(string text, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, GoogleUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["q"] = text }),
        };
        return await SendAsync(request, ParseGoogle, cancellation);
    }

    private async Task<string?> TryMyMemoryAsync(string text, CancellationToken cancellation)
    {
        if (text.Length > MaxMyMemoryLength)
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, MyMemoryUrl + Uri.EscapeDataString(text));
        return await SendAsync(request, ParseMyMemory, cancellation);
    }

    /// <summary>Сбой сервиса или непонятный ответ значит только то, что пора пробовать следующий.</summary>
    private async Task<string?> SendAsync(HttpRequestMessage request, Func<JsonElement, string?> parse, CancellationToken cancellation)
    {
        try
        {
            using var response = await http.SendAsync(request, cancellation);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
            var result = parse(document.RootElement)?.Trim();
            return string.IsNullOrEmpty(result) ? null : result;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    private static string? ParseDeepL(JsonElement root) => root.GetProperty("translations")[0].GetProperty("text").GetString();

    /// <summary>Ответ выглядит как [[["перевод","оригинал",...],...],...]: каждый кусок текста в своём элементе.</summary>
    private static string? ParseGoogle(JsonElement root)
    {
        var result = new StringBuilder();
        foreach (var part in root[0].EnumerateArray())
        {
            result.Append(part[0].GetString());
        }

        return result.ToString();
    }

    private static string? ParseMyMemory(JsonElement root)
    {
        // Об ошибке и лимите этот сервис сообщает текстом прямо в поле перевода.
        return root.GetProperty("responseStatus").GetRawText().Trim('"') == "200"
            ? root.GetProperty("responseData").GetProperty("translatedText").GetString()
            : null;
    }
}
