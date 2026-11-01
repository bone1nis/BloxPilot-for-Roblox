using System.Text;
using System.Text.RegularExpressions;
using RobloxAntiAfk.Core.Abstractions;

namespace RobloxAntiAfk.Core.Services;

/// <summary>
/// Узнаёт об отключении из журнала клиента Roblox: окно при кике остаётся открытым, поэтому по окну этого не видно.
/// При переходе между серверами клиент тоже пишет об отключении, поэтому о нём сообщаем не сразу:
/// сначала ждём, не зайдёт ли клиент на другой сервер. Файлы только читаются.
/// </summary>
public sealed partial class RobloxLogWatcher(string folder, IClock clock) : IDisconnectWatcher
{
    /// <summary>Этот код клиент пишет, когда игрок выходит сам.</summary>
    private const string LeftByPlayerReason = "285";

    private static readonly TimeSpan ConfirmDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TeleportWindow = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan AfterJoinWindow = TimeSpan.FromSeconds(10);

    private string? _file;
    private long _position;
    private string _partialLine = "";
    private DateTimeOffset? _disconnectedAt;
    private DateTimeOffset _ignoreUntil;

    public void Arm()
    {
        _file = FindNewestLog();
        _position = _file is null ? 0 : new FileInfo(_file).Length;
        _partialLine = "";
        _disconnectedAt = null;
        _ignoreUntil = default;
    }

    public bool Poll()
    {
        foreach (var line in ReadNewLines())
        {
            Apply(line);
        }

        if (_disconnectedAt is { } disconnectedAt && clock.Now - disconnectedAt >= ConfirmDelay)
        {
            _disconnectedAt = null;
            return true;
        }

        return false;
    }

    private void Apply(string line)
    {
        var now = clock.Now;
        if (IsTeleportLine(line))
        {
            _disconnectedAt = null;
            _ignoreUntil = now + TeleportWindow;
        }
        else if (IsJoinLine(line))
        {
            // Старый сервер ещё может дописать своё отключение, но настоящее окно перехода на этом закончилось.
            _disconnectedAt = null;
            _ignoreUntil = now + AfterJoinWindow;
        }
        else if (IsDisconnectLine(line) && now >= _ignoreUntil)
        {
            _disconnectedAt ??= now;
        }
    }

    /// <summary>Клиент начал переход на другой сервер. Вид записи зависит от версии клиента, поэтому ищем обе формы.</summary>
    internal static bool IsTeleportLine(string line) =>
        line.Contains("doTeleport: joinScriptUrl", StringComparison.Ordinal) || line.Contains("initiateTeleport", StringComparison.Ordinal);

    /// <summary>Клиент заходит на сервер: значит, прошлая запись об отключении была переходом, а не обрывом.</summary>
    internal static bool IsJoinLine(string line) =>
        line.Contains("! Joining game", StringComparison.Ordinal) || line.Contains("[FLog::Network] Replicator created", StringComparison.Ordinal);

    internal static bool IsDisconnectLine(string line)
    {
        // Точные слова при обрыве интернета не подтверждены, поэтому ищем обе формы, но только в сетевых строках клиента.
        if (line.Contains("FLog::Network", StringComparison.Ordinal)
            && (line.Contains("lost connection", StringComparison.OrdinalIgnoreCase) || line.Contains("connection lost", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var match = DisconnectReason().Match(line);
        return match.Success && match.Groups["reason"].Value != LeftByPlayerReason;
    }

    private List<string> ReadNewLines()
    {
        try
        {
            var newest = FindNewestLog();
            if (newest is null)
            {
                return [];
            }

            if (newest != _file)
            {
                _file = newest;
                _position = 0;
                _partialLine = "";
            }

            return ReadNewLines(newest);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private List<string> ReadNewLines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length <= _position)
        {
            _position = Math.Min(_position, stream.Length);
            return [];
        }

        stream.Position = _position;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var text = _partialLine + reader.ReadToEnd();
        _position = stream.Length;

        var lines = text.Split('\n');
        _partialLine = lines[^1];
        return lines[..^1].ToList();
    }

    private string? FindNewestLog()
    {
        if (!Directory.Exists(folder))
        {
            return null;
        }

        return new DirectoryInfo(folder)
            .EnumerateFiles("*.log")
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;
    }

    [GeneratedRegex(@"Sending disconnect with reason: (?<reason>\d+)")]
    private static partial Regex DisconnectReason();
}
