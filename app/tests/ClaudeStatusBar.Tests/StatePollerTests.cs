using ClaudeStatusBar.Core;
using Xunit;

public class StatePollerTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(),
        "csb_poll_" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public async Task Fires_only_on_ts_change()
    {
        File.WriteAllText(_path, """{"state":"idle","ts":1}""");
        var seen = new List<AppState>();
        using var poller = new StatePoller(new StateReader(_path),
            s => { lock (seen) seen.Add(s); }, periodMs: 30, marshal: a => a());
        poller.Start();

        await Task.Delay(120);                       // varios ticks, mismo ts -> 1 evento
        File.WriteAllText(_path + ".tmp", """{"state":"done","ts":2}""");
        File.Move(_path + ".tmp", _path, overwrite: true);
        await Task.Delay(120);                        // nuevo ts -> 2do evento

        lock (seen)
        {
            Assert.Equal(2, seen.Count);
            Assert.Equal(StatusKind.Idle, seen[0].State);
            Assert.Equal(StatusKind.Done, seen[1].State);
        }
    }

    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }
}
