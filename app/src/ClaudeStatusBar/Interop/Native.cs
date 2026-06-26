using System.Runtime.InteropServices;

namespace ClaudeStatusBar.Interop;

[StructLayout(LayoutKind.Sequential)]
public struct RECT { public int left, top, right, bottom; }

internal static class Native
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter,
                                               string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int MapWindowPoints(IntPtr hWndFrom, IntPtr hWndTo,
                                               ref RECT lpPoints, uint cPoints);

    // ── Embedding constants ──────────────────────────────────────────────────
    internal const int GWL_STYLE   = -16;
    internal const int GWL_EXSTYLE = -20;

    internal const int WS_CHILD       = 0x40000000;
    internal const int WS_POPUP       = unchecked((int)0x80000000);
    internal const int WS_VISIBLE     = 0x10000000;
    internal const int WS_CLIPSIBLINGS = 0x04000000;

    internal const int WS_EX_LAYERED    = 0x00080000;
    internal const int WS_EX_TOOLWINDOW = 0x00000080;
    internal const int WS_EX_NOACTIVATE = 0x08000000;

    internal const uint SWP_NOACTIVATE  = 0x0010;
    internal const uint SWP_SHOWWINDOW  = 0x0040;
    internal const uint SWP_NOZORDER    = 0x0004;

    internal static readonly IntPtr HWND_TOP = IntPtr.Zero;

    // ── Embedding P/Invokes ──────────────────────────────────────────────────
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern uint RegisterWindowMessage(string lpString);

    // ── Audio (MCI) ──────────────────────────────────────────────────────────
    // Plays MP3 via the Media Control Interface — no WPF dispatcher dependency,
    // unlike MediaPlayer, which matters in this mixed WinForms/WPF app.
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    internal static extern int mciSendString(string command,
        System.Text.StringBuilder? returnValue, int returnLength, IntPtr callback);
}
