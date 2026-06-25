// app/src/ClaudeStatusBar/Core/StatusViewModel.cs
namespace ClaudeStatusBar.Core;

public static class StatusViewModel
{
    public static bool ShowPermissionDot(AppState s) => s.State == StatusKind.Permission;

    public static string ShortLabel(AppState s) => s.State switch
    {
        StatusKind.Idle       => "",
        StatusKind.Thinking   => string.IsNullOrEmpty(s.Label) ? "Thinking…" : s.Label,
        StatusKind.Tool       => string.IsNullOrEmpty(s.Label) ? "Using tool" : s.Label,
        StatusKind.Permission => "Permission",
        StatusKind.Done       => "Done",
        _                     => "",
    };

    public static string Elapsed(AppState s, long nowUnix)
    {
        if (s.StartedAt <= 0) return "";
        var secs = Math.Max(0, nowUnix - s.StartedAt);
        return $"{secs / 60:00}:{secs % 60:00}";
    }

    public static string Tooltip(AppState s)
    {
        var parts = new List<string> { $"Claude Code: {ShortLabel(s)}".TrimEnd(':', ' ') };
        if (!string.IsNullOrEmpty(s.Project)) parts.Add($"Project: {s.Project}");
        return string.Join("\n", parts);
    }
}
