using System.Net;
using System.Text;
using RobloxAntiAfk.Core.Services;

namespace RobloxAntiAfk.Core.Tests;

public class OnlineTranslatorTests
{
    private const string GoogleAnswer = """[[["Привет, ","Hello, ",null],["мир","world",null]],null,"en"]""";
    private const string MyMemoryAnswer = """{"responseData":{"translatedText":"Привет мир"},"responseStatus":200}""";
    private const string DeepLAnswer = """{"translations":[{"detected_source_language":"EN","text":"Здравствуй, мир"}]}""";

    private sealed class Route(string hostPart, HttpStatusCode status, string body)
    {
        public string HostPart { get; } = hostPart;
        public HttpStatusCode Status { get; } = status;
        public string Body { get; } = body;
    }

    private sealed class FakeHandler(params Route[] routes) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            var route = routes.FirstOrDefault(item => request.RequestUri!.Host.Contains(item.HostPart, StringComparison.Ordinal));
            return route is null
                ? throw new HttpRequestException("Нет сети")
                : new HttpResponseMessage(route.Status) { Content = new StringContent(route.Body, Encoding.UTF8, "application/json") };
        }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Uses_google_and_joins_the_pieces()
    {
        var handler = new FakeHandler(new Route("googleapis", HttpStatusCode.OK, GoogleAnswer));

        var result = await new OnlineTranslator(new HttpClient(handler)).TranslateAsync("Hello, world", "", Token);

        Assert.Equal("Привет, мир", result);
        Assert.Contains("q=Hello", handler.Bodies[0]);
    }

    [Fact]
    public async Task Falls_back_to_mymemory_when_google_fails()
    {
        var handler = new FakeHandler(
            new Route("googleapis", HttpStatusCode.TooManyRequests, ""),
            new Route("mymemory", HttpStatusCode.OK, MyMemoryAnswer));

        var result = await new OnlineTranslator(new HttpClient(handler)).TranslateAsync("Hello world", "", Token);

        Assert.Equal("Привет мир", result);
    }

    [Fact]
    public async Task Falls_back_when_google_answers_nonsense()
    {
        var handler = new FakeHandler(
            new Route("googleapis", HttpStatusCode.OK, "<html>captcha</html>"),
            new Route("mymemory", HttpStatusCode.OK, MyMemoryAnswer));

        Assert.Equal("Привет мир", await new OnlineTranslator(new HttpClient(handler)).TranslateAsync("Hello world", "", Token));
    }

    [Fact]
    public async Task Limit_message_from_mymemory_is_not_shown_as_a_translation()
    {
        var handler = new FakeHandler(new Route("mymemory", HttpStatusCode.OK, """{"responseData":{"translatedText":"MYMEMORY WARNING: YOU USED ALL AVAILABLE FREE TRANSLATIONS"},"responseStatus":"429"}"""));

        Assert.Null(await new OnlineTranslator(new HttpClient(handler)).TranslateAsync("Hello world", "", Token));
    }

    [Fact]
    public async Task No_network_gives_null_instead_of_an_exception()
    {
        var handler = new FakeHandler();

        Assert.Null(await new OnlineTranslator(new HttpClient(handler)).TranslateAsync("Hello", "", Token));
    }

    [Fact]
    public async Task Key_sends_text_to_deepl_first_and_free_key_uses_the_free_address()
    {
        var handler = new FakeHandler(new Route("deepl", HttpStatusCode.OK, DeepLAnswer));

        var result = await new OnlineTranslator(new HttpClient(handler)).TranslateAsync("Hello", "secret:fx", Token);

        Assert.Equal("Здравствуй, мир", result);
        Assert.Equal("api-free.deepl.com", handler.Requests[0].RequestUri!.Host);
        Assert.Equal("DeepL-Auth-Key secret:fx", handler.Requests[0].Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task Paid_key_uses_the_paid_address_and_bad_key_falls_back()
    {
        var handler = new FakeHandler(
            new Route("deepl", HttpStatusCode.Forbidden, ""),
            new Route("googleapis", HttpStatusCode.OK, GoogleAnswer));

        var result = await new OnlineTranslator(new HttpClient(handler)).TranslateAsync("Hello", "secret", Token);

        Assert.Equal("Привет, мир", result);
        Assert.Equal("api.deepl.com", handler.Requests[0].RequestUri!.Host);
    }

    [Fact]
    public async Task Same_text_is_not_sent_twice()
    {
        var handler = new FakeHandler(new Route("googleapis", HttpStatusCode.OK, GoogleAnswer));
        var translator = new OnlineTranslator(new HttpClient(handler));

        await translator.TranslateAsync("Hello", "", Token);
        await translator.TranslateAsync("Hello", "", Token);

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Failure_is_not_remembered()
    {
        var handler = new FakeHandler();
        var translator = new OnlineTranslator(new HttpClient(handler));

        await translator.TranslateAsync("Hello", "", Token);
        await translator.TranslateAsync("Hello", "", Token);

        Assert.True(handler.Requests.Count > 1);
    }

    [Fact]
    public async Task Very_long_text_is_cut()
    {
        var handler = new FakeHandler(new Route("googleapis", HttpStatusCode.OK, GoogleAnswer));

        await new OnlineTranslator(new HttpClient(handler)).TranslateAsync(new string('a', 5000), "", Token);

        Assert.InRange(handler.Bodies[0].Length, 2000, 2010);
    }
}
