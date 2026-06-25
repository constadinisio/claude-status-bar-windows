using System;

namespace ClaudeStatusBar.Core;

public static class StatusBarPaths
{
    private static string Home =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string StatusBarDir => System.IO.Path.Combine(Home, ".claude", "statusbar");
    public static string StateFile    => System.IO.Path.Combine(StatusBarDir, "state.json");
    public static string SessionsDir  => System.IO.Path.Combine(StatusBarDir, "sessions.d");
}
