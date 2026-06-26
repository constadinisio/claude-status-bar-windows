namespace ClaudeStatusBar.Core;

// Resolves the raw state from state.json into what should actually be shown.
// Esc-cancelling a turn fires NO hook (not even Stop), so state.json freezes on
// thinking/tool/permission. Claude Code writes "interrupted by user" to the
// transcript in that case; we recover off that marker. A large age is an
// absolute safety net (e.g. force-quit, which writes no marker).
public static class EffectiveState
{
    public const int StaleSeconds = 900;

    public static AppState Resolve(AppState raw, long nowUnix, Func<string, string?> lastTranscriptLine)
    {
        if (raw.State is not (StatusKind.Thinking or StatusKind.Tool or StatusKind.Permission))
            return raw;

        if (nowUnix - raw.Ts > StaleSeconds)
            return Reset(raw);

        if (!string.IsNullOrEmpty(raw.Transcript))
        {
            var last = lastTranscriptLine(raw.Transcript);
            if (last is not null && last.Contains("interrupted by user"))
                return Reset(raw);
        }

        return raw;
    }

    private static AppState Reset(AppState s) =>
        s with { State = StatusKind.Idle, Label = "", StartedAt = 0 };
}
