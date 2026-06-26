using System.IO;
using System.Windows.Forms;
using ClaudeStatusBar.Core;
using ClaudeStatusBar.Render;
using ClaudeStatusBar.Sound;

namespace ClaudeStatusBar.App;

public sealed class TrayApplicationContext : ApplicationContext
{
    private IStatusRenderer _renderer;  // mutable: may swap embedded → tray on EmbedLost
    private readonly StatePoller _poller;
    private readonly SynchronizationContext _ui;
    private readonly System.Threading.Timer _sessionTimer;
    private readonly CompletionSound _sound;
    private readonly CompletionChime _chime;
    private AppState _lastState = AppState.Idle;
    private volatile bool _seenSession;
    private volatile bool _exitPosted;

    public TrayApplicationContext(Func<IStatusRenderer> rendererFactory)
    {
        _renderer = rendererFactory();
        _renderer.ExitRequested += (_, _) => ExitThread();
        _renderer.EmbedLost += OnEmbedLost;

        _ui = SynchronizationContext.Current
              ?? throw new InvalidOperationException("No UI sync context");

        _sound = new CompletionSound();
        _chime = new CompletionChime(_sound, () => SoundSetting.IsEnabled);

        _poller = new StatePoller(
            new StateReader(StatusBarPaths.StateFile),
            state =>
            {
                _lastState = state;
                _renderer.Render(state);
                _chime.OnState(state, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            },
            periodMs: 400,
            marshal: action => _ui.Post(_ => action(), null),
            resolve: raw => EffectiveState.Resolve(
                raw, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), TranscriptTail.LastLine));
        _poller.Start();

        // Estado inicial inmediato.
        _renderer.Render(AppState.Idle);

        // Sessions watcher: quit when sessions.d is empty after having seen ≥1 session file.
        _sessionTimer = new System.Threading.Timer(_ => SessionTick(), null, 3000, 2000);
    }

    private void OnEmbedLost(object? sender, EventArgs e)
    {
        _ui.Post(_ => SwapToTray(), null);
    }

    private void SwapToTray()
    {
        if (_renderer is TrayRenderer) return;
        var old = _renderer;
        var tray = new TrayRenderer();
        tray.ExitRequested += (_, _) => ExitThread();
        tray.EmbedLost += OnEmbedLost;     // harmless; tray never raises it
        _renderer = tray;
        old.Dispose();
        _renderer.Render(_lastState);
    }

    private void SessionTick()
    {
        try
        {
            var dir = StatusBarPaths.SessionsDir;
            int count = Directory.Exists(dir) ? Directory.GetFiles(dir).Length : 0;
            if (count > 0)
            {
                _seenSession = true;
                return;
            }
            if (_seenSession && !_exitPosted)
            {
                _exitPosted = true;
                _ui.Post(_ => ExitThread(), null);
            }
        }
        catch { /* ignore transient filesystem errors */ }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sessionTimer.Dispose();
            _poller.Dispose();
            _renderer.Dispose();
            _sound.Dispose();
        }
        base.Dispose(disposing);
    }
}
