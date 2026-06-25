namespace ClaudeStatusBar.Core;

public enum StatusKind { Idle, Thinking, Tool, Permission, Done }

public sealed record AppState(
    StatusKind State,
    string Label,
    string Tool,
    string Project,
    string SessionId,
    string Transcript,
    long StartedAt,
    long Ts)
{
    public static AppState Idle { get; } =
        new(StatusKind.Idle, "", "", "", "", "", 0, 0);
}

public static class StatusKindParser
{
    public static StatusKind Parse(string? raw) => (raw ?? "").Trim().ToLowerInvariant() switch
    {
        "thinking"   => StatusKind.Thinking,
        "tool"       => StatusKind.Tool,
        "permission" => StatusKind.Permission,
        "done"       => StatusKind.Done,
        _            => StatusKind.Idle,
    };
}
