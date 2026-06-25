using System;
using System.Threading;

namespace ClaudeStatusBar.Core;

public sealed class StatePoller : IDisposable
{
    private readonly StateReader _reader;
    private readonly Action<AppState> _onChanged;
    private readonly int _periodMs;
    private readonly Action<Action> _marshal;
    private System.Threading.Timer? _timer;
    private long _lastTs = long.MinValue;
    private int _busy;   // evita reentrancia si un tick se solapa

    public StatePoller(StateReader reader, Action<AppState> onChanged,
                       int periodMs = 400, Action<Action>? marshal = null)
    {
        _reader = reader;
        _onChanged = onChanged;
        _periodMs = periodMs;
        _marshal = marshal ?? (a => a());
    }

    public void Start() =>
        _timer = new System.Threading.Timer(_ => Tick(), null, 0, _periodMs);

    private void Tick()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        try
        {
            var state = _reader.TryRead();
            if (state is null || state.Ts == _lastTs) return;
            _lastTs = state.Ts;
            _marshal(() => _onChanged(state));
        }
        finally { Interlocked.Exchange(ref _busy, 0); }
    }

    public void Dispose() => _timer?.Dispose();
}
