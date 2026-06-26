using ClaudeStatusBar.Core;
using Xunit;

public class EffectiveStateTests
{
    private static AppState Raw(StatusKind kind, long ts, string transcript = "t.jsonl") =>
        AppState.Idle with { State = kind, Label = "Thinking…", Transcript = transcript, StartedAt = 100, Ts = ts };

    [Theory]
    [InlineData(StatusKind.Thinking)]
    [InlineData(StatusKind.Tool)]
    [InlineData(StatusKind.Permission)]
    public void Interrupted_active_state_becomes_idle(StatusKind kind)
    {
        var raw = Raw(kind, ts: 1000);
        var eff = EffectiveState.Resolve(raw, nowUnix: 1001, _ => "...[Request interrupted by user]");

        Assert.Equal(StatusKind.Idle, eff.State);
        Assert.Equal("", eff.Label);
    }

    [Fact]
    public void Active_state_with_normal_transcript_is_unchanged()
    {
        var raw = Raw(StatusKind.Thinking, ts: 1000);
        var eff = EffectiveState.Resolve(raw, nowUnix: 1001, _ => "{\"type\":\"assistant\"}");

        Assert.Equal(StatusKind.Thinking, eff.State);
    }

    [Fact]
    public void Stale_active_state_becomes_idle_regardless_of_transcript()
    {
        var raw = Raw(StatusKind.Thinking, ts: 1000);
        var eff = EffectiveState.Resolve(raw, nowUnix: 1000 + EffectiveState.StaleSeconds + 1, _ => "normal line");

        Assert.Equal(StatusKind.Idle, eff.State);
    }

    [Theory]
    [InlineData(StatusKind.Idle)]
    [InlineData(StatusKind.Done)]
    public void Resting_states_are_never_touched(StatusKind kind)
    {
        var raw = Raw(kind, ts: 1000);
        var eff = EffectiveState.Resolve(raw, nowUnix: 9_999_999, _ => "[Request interrupted by user]");

        Assert.Equal(kind, eff.State);
    }

    [Fact]
    public void Empty_transcript_does_not_invoke_reader_and_stays_active()
    {
        var raw = Raw(StatusKind.Thinking, ts: 1000, transcript: "");
        var eff = EffectiveState.Resolve(raw, nowUnix: 1001,
            _ => throw new Xunit.Sdk.XunitException("reader must not be called for empty transcript"));

        Assert.Equal(StatusKind.Thinking, eff.State);
    }
}
