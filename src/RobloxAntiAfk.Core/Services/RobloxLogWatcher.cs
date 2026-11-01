using System.Text;
using System.Text.RegularExpressions;
using RobloxAntiAfk.Core.Abstractions;

namespace RobloxAntiAfk.Core.Services;

/// <summary>
/// Узнаёт об отключении из журнала клиента Roblox: окно при кике остаётся открытым, поэтому по окну этого не видно.
/// Файлы только читаются.
/// </summary>
public sealed partial class RobloxLogWatcher(string folder) : IDisconnectWatcher
{
    /// <summary>Этот код клиент пишет, когда игрок выходит сам.</summary>
    private const string LeftByPlayerReason = "285";

    private string? _file;
    private long _position;
    private string _partialLine = "";

    public void Arm()
    {
        _file = FindNewestLog();
        _position = _file is null ? 0 : new FileInfo(_file).Length;
        _partialLine = "";
    }

    public bool Poll()
    {
        try
        {
            var newest = FindNewestLog();
            if (newest is null)
            {
                return false;
            }

            if (newest != _file)
            {
                _file = newest;
                _position = 0;
                _partialLine = "";
            }

            return ReadNewLines(newest).Any(IsDisconnectLine);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

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
