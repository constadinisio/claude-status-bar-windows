using System.Windows.Forms;
using ClaudeStatusBar.Core;

namespace ClaudeStatusBar.Render;

public sealed class TrayRenderer : IStatusRenderer
{
    private readonly NotifyIcon _icon;
    private readonly System.Windows.Forms.Timer _anim = new() { Interval = 140 };
    private Icon[] _frames = Array.Empty<Icon>();
    private int _frameIdx;
    private int _iconSize = 16;

    public event EventHandler? ExitRequested;

    public TrayRenderer()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Salir", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _icon = new NotifyIcon
        {
            Visible = true,
            Text = "Claude Code: idle",
            ContextMenuStrip = menu,
            Icon = SystemIcons.Application, // reemplazado en el primer Render
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

        _iconSize = DpiIconSize();
        _frames = IconFactory.FramesFor(state.State, _iconSize);
        _frameIdx = 0;
        _icon.Icon = _frames[0];

        var tip = StatusViewModel.Tooltip(state);
        _icon.Text = tip.Length > 63 ? tip[..63] : tip;  // NotifyIcon.Text máx 64 chars

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
        _anim.Stop(); _anim.Dispose();
        _icon.Visible = false; _icon.Dispose();
        DisposeFrames();
    }
}
