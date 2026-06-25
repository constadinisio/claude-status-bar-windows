using ClaudeStatusBar.Core;
using Xunit;

public class StateReaderTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(),
        "csb_test_" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void TryRead_parses_valid_state()
    {
        File.WriteAllText(_path,
            """{"state":"tool","label":"Running Bash","tool":"Bash","project":"demo","sessionId":"s1","transcript":"/t.jsonl","startedAt":1719000000,"ts":1719000005}""");
        var r = new StateReader(_path).TryRead();
        Assert.NotNull(r);
        Assert.Equal(StatusKind.Tool, r!.State);
        Assert.Equal("Running Bash", r.Label);
        Assert.Equal("Bash", r.Tool);
        Assert.Equal(1719000000, r.StartedAt);
    }

    [Fact]
    public void TryRead_returns_null_when_missing() => Assert.Null(new StateReader(_path).TryRead());

    [Fact]
    public void TryRead_returns_null_on_partial_json()
    {
        File.WriteAllText(_path, """{"state":"too""");
        Assert.Null(new StateReader(_path).TryRead());
    }

    [Fact]
    public void TryRead_does_not_lock_writer()
    {
        File.WriteAllText(_path, """{"state":"idle","ts":1}""");
        var r = new StateReader(_path);
        r.TryRead();
        // El escritor debe poder renombrar/reescribir sin excepción.
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, """{"state":"done","ts":2}""");
        File.Move(tmp, _path, overwrite: true);
        Assert.Equal(StatusKind.Done, r.TryRead()!.State);
    }

    [Fact]
    public void TryRead_returns_null_when_file_locked_exclusively()
    {
        File.WriteAllText(_path, """{"state":"idle","ts":1}""");
        // Hold an exclusive lock so the reader's open fails on every attempt.
        using var locker = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.None);
        // Must return null after exhausting retries, never throw.
        Assert.Null(new StateReader(_path).TryRead());
    }

    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }
}
