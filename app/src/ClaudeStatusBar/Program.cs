using System.Windows.Forms;
using ClaudeStatusBar.App;
using ClaudeStatusBar.Render;

namespace ClaudeStatusBar;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true,
            @"Global\ClaudeStatusBar-9F4C2A77-2C5E-4E2A-9E3A-CSB", out bool createdNew);
        if (!createdNew) return;

        ApplicationConfiguration.Initialize();
        // Por ahora siempre TrayRenderer; en Phase 4 se elige embedded con fallback.
        Application.Run(new TrayApplicationContext(() => new TrayRenderer()));
        GC.KeepAlive(mutex);
    }
}
