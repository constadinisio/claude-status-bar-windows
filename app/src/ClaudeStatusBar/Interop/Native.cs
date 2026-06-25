using System.Runtime.InteropServices;

namespace ClaudeStatusBar.Interop;

internal static class Native
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(IntPtr hIcon);
}
