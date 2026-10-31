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

    [Fact]
    public async Task Bad_token_makes_no_request()
    {
        var (notifier, handler) = Create();

        Assert.False(await notifier.SendAsync("not-a-token", "42", "text"));
        Assert.Null(await notifier.FindChatIdAsync("not-a-token"));

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
        Assert.Null(await notifier.FindChatIdAsync(Token));
    }

    [Fact]
    public async Task Finds_chat_from_the_latest_message()
    {
        const string body = """{"ok":true,"result":[{"message":{"chat":{"id":1}}},{"edited_message":{}},{"message":{"chat":{"id":-200}}}]}""";
        var (notifier, _) = Create(body: body);

        Assert.Equal("-200", await notifier.FindChatIdAsync(Token));
    }

    [Theory]
    [InlineData("""{"ok":true,"result":[]}""")]
    [InlineData("""{"ok":false}""")]
    [InlineData("not json")]
    public async Task No_chat_when_there_is_no_message(string body)
    {
        var (notifier, _) = Create(body: body);

        Assert.Null(await notifier.FindChatIdAsync(Token));
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

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("сеть недоступна");
    }
}
