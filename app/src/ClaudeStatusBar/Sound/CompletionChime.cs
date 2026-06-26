using ClaudeStatusBar.Core;

namespace ClaudeStatusBar.Sound;

// Decides when to play the completion chime: once, on the transition into the
// "done" state, only when the finished turn ran at least MinTurnSeconds and the
// toggle is enabled. The turn's start time is tracked here because update.js
// writes startedAt=0 on "stop", so duration isn't available from the done state.
public sealed class CompletionChime
{
    public const int MinTurnSeconds = 60;

    private readonly ICompletionSound _sound;
    private readonly Func<bool> _isEnabled;

    private long _turnStart;            // startedAt of the active turn (0 = none)
    private StatusKind _prev = StatusKind.Idle;

    public CompletionChime(ICompletionSound sound, Func<bool> isEnabled)
    {
        _sound = sound;
        _isEnabled = isEnabled;
    }

    public void OnState(AppState state, long nowUnix)
    {
        // Remember when the turn began (startedAt is stable across thinking/tool).
        if (state.State is StatusKind.Thinking or StatusKind.Tool && state.StartedAt > 0)
            _turnStart = state.StartedAt;

        // Chime once on the transition into "done", for turns of at least a minute.
        if (state.State == StatusKind.Done && _prev != StatusKind.Done)
        {
            if (_isEnabled() && _turnStart > 0 && nowUnix - _turnStart >= MinTurnSeconds)
                _sound.Play();
            _turnStart = 0;
        }

        _prev = state.State;
    }
}
