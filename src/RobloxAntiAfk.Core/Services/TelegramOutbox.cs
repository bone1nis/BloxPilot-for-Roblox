using RobloxAntiAfk.Core.Abstractions;

namespace RobloxAntiAfk.Core.Services;

/// <summary>
/// Очередь сообщений в Telegram. Главная причина отключения от игры это пропавший интернет,
/// поэтому сообщение нельзя терять при первой неудаче: оно ждёт, пока связь вернётся.
/// Живёт только в памяти. Все методы вызываются из потока интерфейса.
/// </summary>
public sealed class TelegramOutbox(TelegramNotifier notifier, IClock clock)
{
    private const int MaxMessages = 20;
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(6);

    private readonly List<(string Text, DateTimeOffset Added)> _messages = [];
    private DateTimeOffset _nextAttempt;
    private bool _isSending;

    public int PendingCount => _messages.Count;

    public void Add(string text)
    {
        if (_messages.Count >= MaxMessages)
        {
            _messages.RemoveAt(0);
        }

        _messages.Add((text, clock.Now));
    }

    /// <summary>Вызывается раз в секунду и сразу после добавления сообщения. Порядок сообщений сохраняется.</summary>
    public async Task SendPendingAsync(string token, string chatId)
    {
        var now = clock.Now;
        if (_isSending || now < _nextAttempt)
        {
            return;
        }

        _messages.RemoveAll(message => now - message.Added > MaxAge);
        if (_messages.Count == 0 || chatId.Length == 0)
        {
            return;
        }

        _isSending = true;
        try
        {
            while (_messages.Count > 0)
            {
                if (!await notifier.SendAsync(token, chatId, _messages[0].Text))
                {
                    _nextAttempt = clock.Now + RetryInterval;
                    return;
                }

                _messages.RemoveAt(0);
            }
        }
        finally
        {
            _isSending = false;
        }
    }
}
