using RobloxAntiAfk.Core.Services;

namespace RobloxAntiAfk.Core.Tests;

public sealed class RobloxLogWatcherTests : IDisposable
{
    private const string Disconnect = "2026-01-01T12:00:00Z,1.0,abc,6 [FLog::Network] Sending disconnect with reason: 277";
    private const string PlayerLeft = "2026-01-01T12:00:00Z,1.0,abc,6 [FLog::Network] Sending disconnect with reason: 285";
    private const string Ordinary = "2026-01-01T12:00:00Z,1.0,abc,6 [FLog::Output] something else";

    private readonly string _folder = Directory.CreateTempSubdirectory("roblox-log-tests").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string LogFile(string name = "a.log") => Path.Combine(_folder, name);

    private void Append(string line, string name = "a.log") => File.AppendAllText(LogFile(name), line + "\n");

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
        var watcher = new RobloxLogWatcher(_folder);

        watcher.Arm();

        Assert.False(watcher.Poll());
    }

    [Fact]
    public void Each_new_disconnect_is_reported_once()
    {
        Append(Ordinary);
        var watcher = new RobloxLogWatcher(_folder);
        watcher.Arm();

        Append(Disconnect);

        Assert.True(watcher.Poll());
        Assert.False(watcher.Poll());
    }

    [Fact]
    public void Line_written_in_two_parts_is_still_recognised()
    {
        Append(Ordinary);
        var watcher = new RobloxLogWatcher(_folder);
        watcher.Arm();

        File.AppendAllText(LogFile(), "[FLog::Network] Sending disconnect ");
        Assert.False(watcher.Poll());

        File.AppendAllText(LogFile(), "with reason: 277\n");
        Assert.True(watcher.Poll());
    }

    [Fact]
    public void New_log_file_is_read_from_its_start()
    {
        Append(Ordinary);
        var watcher = new RobloxLogWatcher(_folder);
        watcher.Arm();

        Append(Disconnect, "b.log");
        File.SetLastWriteTimeUtc(LogFile(), DateTime.UtcNow.AddHours(-1));

        Assert.True(watcher.Poll());
    }

    [Fact]
    public void Missing_folder_is_not_an_error()
    {
        var watcher = new RobloxLogWatcher(Path.Combine(_folder, "missing"));

        watcher.Arm();

        Assert.False(watcher.Poll());
    }
}
