using System.Windows.Forms;
using ClaudeStatusBar.App;
using ClaudeStatusBar.Core;

namespace ClaudeStatusBar.Render;

public sealed class TrayRenderer : IStatusRenderer
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly System.Windows.Forms.Timer _anim = new() { Interval = 140 };
    private Icon[] _frames = Array.Empty<Icon>();
    private int _frameIdx;
    private bool _disposed;

    public event EventHandler? ExitRequested;
#pragma warning disable CS0067
    public event EventHandler? EmbedLost;
#pragma warning restore CS0067

    public TrayRenderer()
    {
        _menu = new ContextMenuStrip();

        var autoStartItem = new ToolStripMenuItem("Iniciar con Windows")
        {
            Checked = AutoStart.IsEnabled,
        };
        autoStartItem.Click += (_, _) =>
        {
            if (AutoStart.IsEnabled)
                AutoStart.Disable();
            else
                AutoStart.Enable();
            autoStartItem.Checked = AutoStart.IsEnabled;
        };
        _menu.Items.Add(autoStartItem);

        _menu.Items.Add("Salir", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application, // reemplazado en el primer Render
            Text = "Claude Code: idle",
            ContextMenuStrip = _menu,
            Visible = true, // último: NIM_ADD usa Icon/Text ya asignados (evita parpadeo en blanco)
        };
        _anim.Tick += (_, _) =>
        {
            if (_frames.Length == 0) return;
            _frameIdx = (_frameIdx + 1) % _frames.Length;
            _icon.Icon = _frames[_frameIdx];
        };
    }

    public void Render(AppState state)
    {
        _anim.Stop();
        DisposeFrames();

        int iconSize = DpiIconSize();
        _frames = IconFactory.FramesFor(state.State, iconSize);
        _frameIdx = 0;
        if (_frames.Length == 0) return; // defensa: IconFactory siempre devuelve ≥1 frame
        _icon.Icon = _frames[0];

        var tip = StatusViewModel.Tooltip(state);
        _icon.Text = tip.Length > 63 ? tip[..63] : tip;  // NotifyIcon.Text máx 63 chars

        if (_frames.Length > 1) _anim.Start();
    }

    private static int DpiIconSize()
    {
        using var g = Graphics.FromHwnd(IntPtr.Zero);
        return (int)Math.Round(16 * g.DpiX / 96f);
    }

    private void DisposeFrames()
    {
        foreach (var f in _frames) f.Dispose();
        _frames = Array.Empty<Icon>();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _anim.Stop(); _anim.Dispose();
        _icon.Visible = false; _icon.Dispose();
        _menu.Dispose();
        DisposeFrames();
    }
}
