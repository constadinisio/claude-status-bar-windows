// app/src/ClaudeStatusBar/Interop/TaskbarLocator.cs
using System.Diagnostics;

namespace ClaudeStatusBar.Interop;

public static class TaskbarLocator
{
    /// <summary>
    /// Returns the HWND of the primary Shell_TrayWnd owned by explorer.exe,
    /// or IntPtr.Zero if not found.
    /// </summary>
    public static IntPtr FindPrimaryTaskbar()
    {
        // FindWindow returns the first Shell_TrayWnd; third-party apps may create
        // same-named windows, so we validate the owner process is explorer.exe.
        IntPtr h = Native.FindWindow("Shell_TrayWnd", null);
        if (h == IntPtr.Zero) return IntPtr.Zero;

        Native.GetWindowThreadProcessId(h, out uint pid);
        try
        {
            using var p = Process.GetProcessById((int)pid);
            if (p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                return h;
        }
        catch (ArgumentException) { /* process exited between calls */ }

        return h; // fallback: only one found, return it regardless
    }

    /// <summary>
    /// Computes a rect in taskbar CLIENT coordinates, widthPx wide, positioned
    /// immediately to the left of the TrayNotifyWnd (system-tray area).
    /// Returns false if the taskbar handle is invalid or the rect would be empty.
    /// </summary>
    public static bool TryGetEmbedBounds(IntPtr taskbar, int widthPx, out Native.RECT clientRect)
    {
        clientRect = default;
        if (taskbar == IntPtr.Zero) return false;
        if (!Native.GetClientRect(taskbar, out var tb)) return false;

        int height = tb.bottom - tb.top;
        int rightEdge = tb.right;

        // Find TrayNotifyWnd (clock + notification icons) and use its left edge
        // as the right boundary for our embed rect.
        IntPtr tray = Native.FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        if (tray != IntPtr.Zero && Native.GetWindowRect(tray, out var trayScreen))
        {
            // MapWindowPoints converts from screen coordinates to taskbar client coordinates.
            // cPoints=2 treats the RECT as two POINT values (left/top, right/bottom).
            Native.MapWindowPoints(IntPtr.Zero, taskbar, ref trayScreen, 2);
            rightEdge = trayScreen.left;
        }

        const int margin = 8;
        int right = rightEdge - margin;
        int left = Math.Max(tb.left, right - widthPx);
        clientRect = new Native.RECT
        {
            left   = left,
            top    = tb.top + 2,
            right  = right,
            bottom = tb.bottom - 2
        };
        return (clientRect.right - clientRect.left) > 0 && height > 0;
    }
}
