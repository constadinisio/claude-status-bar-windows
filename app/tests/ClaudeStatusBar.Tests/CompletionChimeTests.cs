using ClaudeStatusBar.Core;
using ClaudeStatusBar.Sound;
using Xunit;

public class CompletionChimeTests
{
    private sealed class FakeSound : ICompletionSound
    {
        public int Plays { get; private set; }
        public void Play() => Plays++;
    }

    private static AppState Thinking(long startedAt) =>
        AppState.Idle with { State = StatusKind.Thinking, StartedAt = startedAt };

    private static AppState Done() =>
        AppState.Idle with { State = StatusKind.Done, StartedAt = 0 };

    [Fact]
    public void Plays_once_when_a_long_turn_finishes()
    {
        var sound = new FakeSound();
        var chime = new CompletionChime(sound, () => true);

        chime.OnState(Thinking(100), 100);
        chime.OnState(Done(), 170); // 70s turn

        Assert.Equal(1, sound.Plays);
    }

    [Fact]
    public void Does_not_play_for_a_short_turn()
    {
        var sound = new FakeSound();
        var chime = new CompletionChime(sound, () => true);

        chime.OnState(Thinking(100), 100);
        chime.OnState(Done(), 130); // 30s turn

        Assert.Equal(0, sound.Plays);
    }

    [Fact]
    public void Does_not_replay_while_staying_done()
    {
        var sound = new FakeSound();
        var chime = new CompletionChime(sound, () => true);

        chime.OnState(Thinking(100), 100);
        chime.OnState(Done(), 200);
        chime.OnState(Done(), 201); // still done — no new transition

        Assert.Equal(1, sound.Plays);
    }

    [Fact]
    public void Never_plays_when_disabled()
    {
        var sound = new FakeSound();
        var chime = new CompletionChime(sound, () => false);

        chime.OnState(Thinking(100), 100);
        chime.OnState(Done(), 300);

        Assert.Equal(0, sound.Plays);
    }

    [Fact]
    public void Does_not_play_when_done_arrives_without_a_tracked_turn()
    {
        var sound = new FakeSound();
        var chime = new CompletionChime(sound, () => true);

        chime.OnState(Done(), 9999); // no preceding thinking/tool

        Assert.Equal(0, sound.Plays);
    }
}
