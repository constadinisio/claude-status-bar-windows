namespace ClaudeStatusBar.Interop;

/// <summary>
/// Reparents a child HWND into Shell_TrayWnd and keeps it positioned at the
/// right edge of the taskbar (left of the notification tray).
/// Call <see cref="ReEmbed"/> when a "TaskbarCreated" broadcast is received.
/// </summary>
public sealed class WindowEmbedder : IDisposable
{
    private IntPtr _child;
    private IntPtr _taskbar;
    private int    _widthPx;

    public event EventHandler? Detached;

    /// <summary>
    /// Locates the primary taskbar, applies child-window styles to
    /// <paramref name="child"/>, calls SetParent, then positions the window.
    /// Returns false (never throws) if the taskbar is not found or SetParent fails.
    /// </summary>
    public bool TryEmbed(IntPtr child, int widthPx)
    {
        _child   = child;
        _widthPx = widthPx;
        _taskbar = TaskbarLocator.FindPrimaryTaskbar();

        if (_taskbar == IntPtr.Zero) return false;

        // Set window style: clear WS_POPUP, add WS_CHILD | WS_CLIPSIBLINGS | WS_VISIBLE.
        // Styles must be updated before SetParent so the child is accepted as a child window.
        var style = (long)Native.GetWindowLongPtr(child, Native.GWL_STYLE);
        style = (style & ~(long)Native.WS_POPUP) | Native.WS_CHILD | Native.WS_CLIPSIBLINGS | Native.WS_VISIBLE;
        Native.SetWindowLongPtr(child, Native.GWL_STYLE, (IntPtr)style);

        // Set extended style: add WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW.
        var ex = (long)Native.GetWindowLongPtr(child, Native.GWL_EXSTYLE);
        ex |= Native.WS_EX_LAYERED | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW;
        Native.SetWindowLongPtr(child, Native.GWL_EXSTYLE, (IntPtr)ex);

        // Reparent into the taskbar.
        if (Native.SetParent(child, _taskbar) == IntPtr.Zero) return false;

        Reposition();
        return true;
    }

    /// <summary>
    /// Moves and sizes the child window to its embed rect within the taskbar.
    /// No-ops silently if the child or taskbar handle is not set.
    /// </summary>
    public void Reposition()
    {
        if (_child == IntPtr.Zero || _taskbar == IntPtr.Zero) return;
        if (!TaskbarLocator.TryGetEmbedBounds(_taskbar, _widthPx, out var r)) return;

        Native.SetWindowPos(
            _child,
            Native.HWND_TOP,
            r.left, r.top, r.right - r.left, r.bottom - r.top,
            Native.SWP_SHOWWINDOW | Native.SWP_NOACTIVATE | Native.SWP_NOZORDER);
    }

    /// <summary>
    /// Re-runs <see cref="TryEmbed"/> after a TaskbarCreated broadcast.
    /// Raises <see cref="Detached"/> if the taskbar can no longer be found.
    /// </summary>
    public void ReEmbed()
    {
        if (_child != IntPtr.Zero && !TryEmbed(_child, _widthPx))
            Detached?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The child window's lifetime is managed by its owner; nothing to release here.
    /// </summary>
    public void Dispose() { }
}
