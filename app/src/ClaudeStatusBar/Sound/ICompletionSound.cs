namespace ClaudeStatusBar.Sound;

// Plays the completion chime. Abstracted so the decision logic (CompletionChime)
// can be unit-tested without producing audio.
public interface ICompletionSound
{
    void Play();
}
