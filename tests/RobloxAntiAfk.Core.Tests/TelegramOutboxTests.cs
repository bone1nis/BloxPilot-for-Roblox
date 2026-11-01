using System.Net;
using RobloxAntiAfk.Core.Services;
using RobloxAntiAfk.Core.Tests.Fakes;

namespace RobloxAntiAfk.Core.Tests;

public class TelegramOutboxTests
{
    private const string Token = "123456:ABCdefGHIjklMNOpqrSTUvwx";

    private sealed class SwitchableHandler : HttpMessageHandler
    {
        public bool IsOnline { get; set; }
        public int Attempts { get; private set; }
        public List<string> Sent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Attempts++;
            if (!IsOnline)
            {
                throw new HttpRequestException("Нет сети");
            }

            Sent.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private readonly FakeClock _clock = new();
    private readonly SwitchableHandler _handler = new();
    private readonly TelegramOutbox _outbox;

    public TelegramOutboxTests() => _outbox = new TelegramOutbox(new TelegramNotifier(new HttpClient(_handler)), _clock);

    private Task Send() => _outbox.SendPendingAsync(Token, "42");

    [Fact]
    public async Task Message_written_without_internet_is_delivered_when_it_returns()
    {
        _outbox.Add("отключился");
        await Send();
        Assert.Equal(1, _outbox.PendingCount);

        _handler.IsOnline = true;
        _clock.Advance(TimeSpan.FromSeconds(16));
        await Send();

        Assert.Equal(0, _outbox.PendingCount);
        Assert.Single(_handler.Sent);
    }

    [Fact]
    public async Task Does_not_hammer_the_network_between_retries()
    {
        _outbox.Add("отключился");
        await Send();
        await Send();

        _clock.Advance(TimeSpan.FromSeconds(5));
        await Send();

        Assert.Equal(1, _handler.Attempts);
    }

    [Fact]
    public async Task Messages_keep_their_order()
    {
        _handler.IsOnline = false;
        _outbox.Add("первое");
        _outbox.Add("второе");
        await Send();

        _handler.IsOnline = true;
        _clock.Advance(TimeSpan.FromSeconds(16));
        await Send();

        Assert.Equal(2, _handler.Sent.Count);
        Assert.Contains(System.Text.Json.JsonEncodedText.Encode("первое").ToString(), _handler.Sent[0]);
        Assert.Contains(System.Text.Json.JsonEncodedText.Encode("второе").ToString(), _handler.Sent[1]);
    }

    [Fact]
    public async Task Very_old_messages_are_dropped()
    {
        _outbox.Add("отключился");
        await Send();

        _handler.IsOnline = true;
        _clock.Advance(TimeSpan.FromHours(7));
        await Send();

        Assert.Equal(0, _outbox.PendingCount);
        Assert.Empty(_handler.Sent);
    }

    [Fact]
    public void Queue_has_a_limit()
    {
        for (var index = 0; index < 50; index++)
        {
            _outbox.Add("сообщение");
        }

        Assert.Equal(20, _outbox.PendingCount);
    }

    [Fact]
    public async Task Nothing_is_sent_until_a_chat_is_connected()
    {
        _handler.IsOnline = true;
        _outbox.Add("отключился");

        await _outbox.SendPendingAsync(Token, "");

        Assert.Equal(0, _handler.Attempts);
        Assert.Equal(1, _outbox.PendingCount);
    }
}
