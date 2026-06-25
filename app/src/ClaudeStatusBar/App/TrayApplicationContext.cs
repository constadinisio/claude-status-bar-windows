using System.Windows.Forms;
using ClaudeStatusBar.Core;
using ClaudeStatusBar.Render;

namespace ClaudeStatusBar.App;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly IStatusRenderer _renderer;
    private readonly StatePoller _poller;

    public TrayApplicationContext(Func<IStatusRenderer> rendererFactory)
    {
        _renderer = rendererFactory();
        _renderer.ExitRequested += (_, _) => ExitThread();

        var ui = WindowsFormsSynchronizationContext.Current
                 ?? throw new InvalidOperationException("No UI sync context");
        _poller = new StatePoller(
            new StateReader(StatusBarPaths.StateFile),
            state => _renderer.Render(state),
            periodMs: 400,
            marshal: action => ui.Post(_ => action(), null));
        _poller.Start();

        // Estado inicial inmediato.
        _renderer.Render(AppState.Idle);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _poller.Dispose(); _renderer.Dispose(); }
        base.Dispose(disposing);
    }
}
