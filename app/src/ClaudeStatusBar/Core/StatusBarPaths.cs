using System;
using System.IO;

namespace ClaudeStatusBar.Core;

public static class StatusBarPaths
{
    private static string Home =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string StatusBarDir => Path.Combine(Home, ".claude", "statusbar");
    public static string StateFile    => Path.Combine(StatusBarDir, "state.json");
    public static string SessionsDir  => Path.Combine(StatusBarDir, "sessions.d");
}
