using RobloxAntiAfk.Core.Services;
using RobloxAntiAfk.Core.Tests.Fakes;

namespace RobloxAntiAfk.Core.Tests;

public sealed class RobloxLogWatcherTests : IDisposable
{
    private const string Disconnect = "2026-01-01T12:00:00Z,1.0,abc,6 [FLog::Network] Sending disconnect with reason: 277";
    private const string PlayerLeft = "2026-01-01T12:00:00Z,1.0,abc,6 [FLog::Network] Sending disconnect with reason: 285";
    private const string Ordinary = "2026-01-01T12:00:00Z,1.0,abc,6 [FLog::Output] something else";

    private const string Teleport = "2026-01-01T12:00:00Z,1.0,abc,6 [FLog::UgcExperienceController] UgcExperienceController: doTeleport: joinScriptUrl = x";
    private const string OlderTeleport = "2026-01-01T12:00:00Z,1.0,abc,6 [FLog::SingleSurfaceApp] initiateTeleport";
    private const string Joining = "2026-01-01T12:00:00Z,1.0,abc,6 [FLog::Output] ! Joining game 'x' place 1 at 1.1.1.1";
    private const string NoResponse = "2026-01-01T12:00:00Z,1.0,abc,6,Error [DFLog::NetworkClient] Failed to connect to server at 1.1.1.1|5000, no response.";
    private const string SessionError = "2026-01-01T12:00:00Z,1.0,abc,6 [FLog::Output] Session Transition FSM: Error Occurred.";
    private static readonly TimeSpan ConfirmDelay = TimeSpan.FromSeconds(30);

    private readonly string _folder = Directory.CreateTempSubdirectory("roblox-log-tests").FullName;
    private readonly FakeClock _clock = new();

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string LogFile(string name = "a.log") => Path.Combine(_folder, name);

    private void Append(string line, string name = "a.log") => File.AppendAllText(LogFile(name), line + "\n");

    private RobloxLogWatcher ArmedWatcher()
    {
        Append(Ordinary);
        var watcher = new RobloxLogWatcher(_folder, _clock);
        watcher.Arm();
        return watcher;
    }

    /// <summary>Читает журнал, ждёт, пока отключение подтвердится, и проверяет ещё раз.</summary>
    private bool PollAfterDelay(RobloxLogWatcher watcher)
    {
        Assert.False(watcher.Poll());
        _clock.Advance(ConfirmDelay);
        return watcher.Poll();
    }

    [Theory]
    [InlineData(Disconnect, true)]
    [InlineData(PlayerLeft, false)]
    [InlineData(Ordinary, false)]
    [InlineData("[FLog::Network] Lost connection with reason : Lost connection to the game server", true)]
    [InlineData("[FLog::Network] Connection lost - Cannot contact server/client", true)]
    [InlineData("[FLog::Output] Player chat: I lost connection yesterday", false)]
    public void Recognises_disconnect_lines(string line, bool expected) =>
        Assert.Equal(expected, RobloxLogWatcher.IsDisconnectLine(line));

    [Fact]
    public void Disconnects_written_before_arming_do_not_count()
    {
        Append(Disconnect);
        var watcher = new RobloxLogWatcher(_folder, _clock);

        watcher.Arm();

        Assert.False(PollAfterDelay(watcher));
    }

    [Fact]
    public void Each_new_disconnect_is_reported_once()
    {
        Append(Ordinary);
        var watcher = new RobloxLogWatcher(_folder, _clock);
        watcher.Arm();

        Append(Disconnect);

        Assert.True(PollAfterDelay(watcher));
        _clock.Advance(ConfirmDelay);
        Assert.False(watcher.Poll());
    }

    [Fact]
    public void Line_written_in_two_parts_is_still_recognised()
    {
        Append(Ordinary);
        var watcher = new RobloxLogWatcher(_folder, _clock);
        watcher.Arm();

        File.AppendAllText(LogFile(), "[FLog::Network] Sending disconnect ");
        Assert.False(watcher.Poll());

        File.AppendAllText(LogFile(), "with reason: 277\n");
        Assert.True(PollAfterDelay(watcher));
    }

    [Fact]
    public void New_log_file_is_read_from_its_start()
    {
        Append(Ordinary);
        var watcher = new RobloxLogWatcher(_folder, _clock);
        watcher.Arm();

        Append(Disconnect, "b.log");
        File.SetLastWriteTimeUtc(LogFile(), DateTime.UtcNow.AddHours(-1));

        Assert.True(PollAfterDelay(watcher));
    }

    [Theory]
    [InlineData(Teleport)]
    [InlineData(OlderTeleport)]
    public void Moving_to_another_server_is_not_a_disconnect(string teleportLine)
    {
        var watcher = ArmedWatcher();

        Append(teleportLine);
        Append(Disconnect);

        Assert.False(PollAfterDelay(watcher));
    }

    [Fact]
    public void Joining_soon_after_a_disconnect_cancels_it_even_without_a_teleport_line()
    {
        var watcher = ArmedWatcher();

        Append(Disconnect);
        Assert.False(watcher.Poll());
        _clock.Advance(TimeSpan.FromSeconds(10));
        Append(Joining);

        Assert.False(PollAfterDelay(watcher));
    }

    [Fact]
    public void Old_server_finishing_right_after_the_join_does_not_count()
    {
        var watcher = ArmedWatcher();

        Append(Joining);
        Append(Disconnect);

        Assert.False(PollAfterDelay(watcher));
    }

    [Fact]
    public void Disconnect_later_on_the_new_server_is_reported()
    {
        var watcher = ArmedWatcher();
        Append(Teleport);
        Append(Disconnect);
        Append(Joining);
        Assert.False(PollAfterDelay(watcher));

        Append(Disconnect);

        Assert.True(PollAfterDelay(watcher));
    }

    [Fact]
    public void Failed_teleport_stops_hiding_disconnects_after_a_while()
    {
        var watcher = ArmedWatcher();
        Append(Teleport);
        Assert.False(watcher.Poll());
        _clock.Advance(TimeSpan.FromSeconds(61));

        Append(Disconnect);

        Assert.True(PollAfterDelay(watcher));
    }

    /// <summary>Порядок записей как в настоящем журнале при ошибке 279: переход, код 285, заход, через 20 секунд отказ.</summary>
    [Theory]
    [InlineData(NoResponse)]
    [InlineData(SessionError)]
    public void Failing_to_reach_the_new_server_right_after_a_teleport_is_reported(string failureLine)
    {
        var watcher = ArmedWatcher();
        Append(Teleport);
        Append(PlayerLeft);
        Append(Joining);
        Assert.False(watcher.Poll());
        _clock.Advance(TimeSpan.FromSeconds(20));

        Append(failureLine);

        Assert.True(PollAfterDelay(watcher));
    }

    [Fact]
    public void Connecting_after_retry_cancels_the_failure()
    {
        var watcher = ArmedWatcher();
        Append(NoResponse);
        Assert.False(watcher.Poll());
        _clock.Advance(TimeSpan.FromSeconds(10));

        Append(Joining);

        Assert.False(PollAfterDelay(watcher));
    }

    [Fact]
    public void Failure_is_reported_once_even_with_both_lines()
    {
        var watcher = ArmedWatcher();

        Append(NoResponse);
        Append(SessionError);

        Assert.True(PollAfterDelay(watcher));
        _clock.Advance(ConfirmDelay);
        Assert.False(watcher.Poll());
    }

    [Fact]
    public void Missing_folder_is_not_an_error()
    {
        var watcher = new RobloxLogWatcher(Path.Combine(_folder, "missing"), _clock);

        watcher.Arm();

        Assert.False(watcher.Poll());
    }
}
