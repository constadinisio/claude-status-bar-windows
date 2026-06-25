// app/src/ClaudeStatusBar/Render/EmbeddedRenderer.cs
using System.Windows.Forms;
using System.Windows.Interop;
using ClaudeStatusBar.Core;
using ClaudeStatusBar.Interop;

namespace ClaudeStatusBar.Render;

public sealed class EmbeddedRenderer : IStatusRenderer
{
    private readonly Widget _widget;
    private readonly WindowEmbedder _embedder = new();
    private readonly uint _taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
    private const int WidthPx = 200;

    // ExitRequested is interface-required; the embedded widget has no quit UI.
#pragma warning disable CS0067
    public event EventHandler? ExitRequested;
#pragma warning restore CS0067
    public event EventHandler? EmbedLost;

    public EmbeddedRenderer()
    {
        // TrayApplicationContext requires WindowsFormsSynchronizationContext.Current.
        // With TrayRenderer the NotifyIcon ctor auto-installs it; the embedded renderer
        // creates no WinForms controls, so we ensure it explicitly here.
        if (SynchronizationContext.Current is not WindowsFormsSynchronizationContext)
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

        // WPF's Dispatcher installs a DispatcherSynchronizationContext when the first
        // Window is constructed.  Save and restore the WinForms context around that call.
        var prevCtx = SynchronizationContext.Current;
        _widget = new Widget();
        SynchronizationContext.SetSynchronizationContext(prevCtx);

        // Subscribe once here (not in Embed) so a retried Embed() doesn't fire EmbedLost N times.
        _embedder.Detached += (_, _) => EmbedLost?.Invoke(this, EventArgs.Empty);
    }

    public bool Embed()
    {
        // Show() may also touch SynchronizationContext; guard it the same way.
        var prevCtx = SynchronizationContext.Current;
        _widget.Show();
        SynchronizationContext.SetSynchronizationContext(prevCtx);

        var hwnd = _widget.Handle;

        var src = HwndSource.FromHwnd(hwnd);
        if (src is null) return false;   // can't hook TaskbarCreated -> report embed failure for fallback
        src.AddHook(WndProc);

        return _embedder.TryEmbed(hwnd, WidthPx);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if ((uint)msg == _taskbarCreated) _embedder.ReEmbed();
        return IntPtr.Zero;
    }

    public void Render(AppState state)
    {
        if (!_widget.Dispatcher.CheckAccess())
        { _widget.Dispatcher.Invoke(() => Render(state)); return; }
        _widget.Update(state);
        _embedder.Reposition();
    }

    public void Dispose()
    {
        // Close the WPF widget first (destroys the HWND, killing the message source),
        // then dispose the embedder, to avoid a WndProc->ReEmbed-on-disposed-embedder race.
        if (_widget.Dispatcher.CheckAccess()) _widget.Close();
        else _widget.Dispatcher.Invoke(_widget.Close);
        _embedder.Dispose();
    }
}
