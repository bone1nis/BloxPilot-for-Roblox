namespace RobloxAntiAfk.Core.Abstractions;

/// <summary>Время и ожидание. В тестах подменяется, чтобы не ждать по-настоящему.</summary>
public interface IClock
{
    DateTimeOffset Now { get; }

    Task Delay(TimeSpan duration);

    /// <summary>Откладывает продолжение на следующий проход цикла интерфейса.</summary>
    Task Yield();
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;

    public Task Delay(TimeSpan duration) => Task.Delay(duration);

    public async Task Yield() => await Task.Yield();
}
