using System.Net;
using System.Text;
using RobloxAntiAfk.Core.Services;

namespace RobloxAntiAfk.Core.Tests;

public class TelegramNotifierTests
{
    private const string Token = "123456:ABCdefGHIjklMNOpqrSTUvwx";

    private static (TelegramNotifier Notifier, FakeHandler Handler) Create(HttpStatusCode status = HttpStatusCode.OK, string body = "{}")
    {
        var handler = new FakeHandler(status, body);
        return (new TelegramNotifier(new HttpClient(handler)), handler);
    }

    [Theory]
    [InlineData(Token, true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("123456", false)]
    [InlineData("123456:short", false)]
    [InlineData("123456:ABCdefGHIjklMNOpqrSTUvwx/../../evil", false)]
    public void Validates_token_format(string? token, bool expected) =>
        Assert.Equal(expected, TelegramNotifier.IsValidToken(token));

    [Fact]
    public async Task Sends_message_only_to_the_telegram_api()
    {
        var (notifier, handler) = Create();

        Assert.True(await notifier.SendAsync(Token, "42", "привет"));

        Assert.Equal($"https://api.telegram.org/bot{Token}/sendMessage", handler.LastUri);
        Assert.Contains("\"chat_id\":\"42\"", handler.LastBody);
        Assert.Contains("\"text\":", handler.LastBody);
    }

    [Theory]
    [InlineData("https://relay.example.workers.dev", true)]
    [InlineData("https://relay.example.workers.dev/", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("relay.example.workers.dev", false)]
    [InlineData("http://relay.example.workers.dev", false)]
    [InlineData("https://relay.example.workers.dev?a=1", false)]
    public void Relay_must_be_a_full_https_address(string? url, bool expected) =>
        Assert.Equal(expected, TelegramNotifier.IsValidRelayUrl(url));

    [Theory]
    [InlineData("https://relay.example.workers.dev")]
    [InlineData("https://relay.example.workers.dev/")]
    public async Task Relay_receives_the_same_requests_as_telegram(string relay)
    {
        var (notifier, handler) = Create(body: """{"ok":true,"result":[{"message":{"chat":{"id":42}}}]}""");

        notifier.UseRelay(relay);
        Assert.True(notifier.IsUsingRelay);

        Assert.True(await notifier.SendAsync(Token, "42", "привет"));
        Assert.Equal($"https://relay.example.workers.dev/bot{Token}/sendMessage", handler.LastUri);

        Assert.Equal("42", (await notifier.FindChatAsync(Token)).ChatId);
        Assert.Equal($"https://relay.example.workers.dev/bot{Token}/getUpdates", handler.LastUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://relay.example.workers.dev")]
    public async Task Without_a_usable_relay_messages_go_straight_to_telegram(string relay)
    {
        var (notifier, handler) = Create();
        notifier.UseRelay("https://relay.example.workers.dev");

        notifier.UseRelay(relay);

        Assert.False(notifier.IsUsingRelay);
        Assert.True(await notifier.SendAsync(Token, "42", "привет"));
        Assert.Equal($"https://api.telegram.org/bot{Token}/sendMessage", handler.LastUri);
    }

    [Fact]
    public void Relay_address_survives_settings_reset_and_is_trimmed()
    {
        var settings = new RobloxAntiAfk.Core.Domain.AppSettings { TelegramRelayUrl = " https://relay.example.workers.dev " }.Normalized();

        Assert.Equal("https://relay.example.workers.dev", settings.TelegramRelayUrl);
        Assert.Equal(settings.TelegramRelayUrl, settings.ToDefaultsKeepingUserData().TelegramRelayUrl);
    }

    [Fact]
    public async Task Bad_token_makes_no_request()
    {
        var (notifier, handler) = Create();

        Assert.False(await notifier.SendAsync("not-a-token", "42", "text"));
        Assert.Equal(TelegramLookupStatus.TokenRejected, (await notifier.FindChatAsync("not-a-token")).Status);

        Assert.Null(handler.LastUri);
    }

    [Fact]
    public async Task Server_error_is_a_failure()
    {
        var (notifier, _) = Create(HttpStatusCode.Unauthorized);

        Assert.False(await notifier.SendAsync(Token, "42", "text"));
    }

    [Fact]
    public async Task Network_error_is_a_failure_and_does_not_throw()
    {
        var notifier = new TelegramNotifier(new HttpClient(new FailingHandler()));

        Assert.False(await notifier.SendAsync(Token, "42", "text"));
        Assert.Equal(TelegramLookupStatus.Unreachable, (await notifier.FindChatAsync(Token)).Status);
    }

    [Fact]
    public async Task Finds_chat_from_the_latest_message()
    {
        const string body = """{"ok":true,"result":[{"message":{"chat":{"id":1}}},{"edited_message":{}},{"message":{"chat":{"id":-200}}}]}""";
        var (notifier, _) = Create(body: body);

        var lookup = await notifier.FindChatAsync(Token);

        Assert.Equal(TelegramLookupStatus.Found, lookup.Status);
        Assert.Equal("-200", lookup.ChatId);
    }

    [Theory]
    [InlineData("""{"ok":true,"result":[{"edited_message":{"chat":{"id":7}}}]}""")]
    [InlineData("""{"ok":true,"result":[{"my_chat_member":{"chat":{"id":7}}}]}""")]
    public async Task Finds_chat_from_other_updates_of_the_user(string body)
    {
        var (notifier, _) = Create(body: body);

        Assert.Equal("7", (await notifier.FindChatAsync(Token)).ChatId);
    }

    [Fact]
    public async Task No_messages_names_the_bot_so_the_user_can_check_it()
    {
        var notifier = new TelegramNotifier(new HttpClient(new ByMethodHandler(
            """{"ok":true,"result":[]}""",
            """{"ok":true,"result":{"username":"my_bot"}}""")));

        var lookup = await notifier.FindChatAsync(Token);

        Assert.Equal(TelegramLookupStatus.NoMessages, lookup.Status);
        Assert.Equal("my_bot", lookup.BotName);
        Assert.Null(lookup.ChatId);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, TelegramLookupStatus.TokenRejected)]
    [InlineData(HttpStatusCode.NotFound, TelegramLookupStatus.TokenRejected)]
    [InlineData(HttpStatusCode.Conflict, TelegramLookupStatus.Conflict)]
    [InlineData(HttpStatusCode.BadGateway, TelegramLookupStatus.Unreachable)]
    public async Task Failure_reason_comes_from_the_status_code(HttpStatusCode status, TelegramLookupStatus expected)
    {
        var (notifier, _) = Create(status);

        Assert.Equal(expected, (await notifier.FindChatAsync(Token)).Status);
    }

    [Theory]
    [InlineData("""{"ok":false}""")]
    [InlineData("not json")]
    public async Task Strange_answer_means_telegram_was_not_reached(string body)
    {
        var (notifier, _) = Create(body: body);

        Assert.Equal(TelegramLookupStatus.Unreachable, (await notifier.FindChatAsync(Token)).Status);
    }

    [Fact]
    public async Task Client_connects_when_only_one_address_family_answers()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var cancellation = TestContext.Current.CancellationToken;
        var server = Task.Run(
            async () =>
            {
                using var client = await listener.AcceptTcpClientAsync(cancellation);
                var stream = client.GetStream();
                _ = await stream.ReadAsync(new byte[1024], cancellation);
                await stream.WriteAsync("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray(), cancellation);
            },
            cancellation);
        using var http = TelegramNotifier.CreateHttpClient(TimeSpan.FromSeconds(10));

        using var response = await http.GetAsync($"http://localhost:{port}/", cancellation);

        Assert.True(response.IsSuccessStatusCode);
        await server;
    }

    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? LastUri { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri?.ToString();
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class ByMethodHandler(string updates, string me) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.RequestUri!.AbsolutePath.EndsWith("/getMe", StringComparison.Ordinal) ? me : updates;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("сеть недоступна");
    }
}
