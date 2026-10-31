using System.Security;
using RobloxAntiAfk.Core.Abstractions;

namespace RobloxAntiAfk.Platform.Mac;

/// <summary>Обычный агент пользователя: файл в LaunchAgents, система подхватит его при следующем входе.</summary>
internal sealed class MacAutoStart : IAutoStart
{
    private const string Label = "local.roblox.antiafk.autostart";

    private static string AgentPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", $"{Label}.plist");

    public void Apply(bool isEnabled)
    {
        if (isEnabled && Environment.ProcessPath is { } path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AgentPath)!);
            File.WriteAllText(AgentPath, BuildAgent(path));
        }
        else
        {
            File.Delete(AgentPath);
        }
    }

    internal static string BuildAgent(string executable) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
          <key>Label</key>
          <string>{Label}</string>
          <key>ProgramArguments</key>
          <array>
            <string>{SecurityElement.Escape(executable)}</string>
            <string>--autostart</string>
          </array>
          <key>RunAtLoad</key>
          <true/>
        </dict>
        </plist>
        """;
}
