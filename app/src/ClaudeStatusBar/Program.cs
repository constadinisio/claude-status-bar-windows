using System.Windows.Forms;
using ClaudeStatusBar.App;
using ClaudeStatusBar.Render;

namespace ClaudeStatusBar;

internal static class Program
{
    private const string UpdateUrl =
        "https://github.com/constadinisio/claude-status-bar-windows";

    [STAThread]
    private static void Main()
    {
        // MUST be the very first statement — handles Velopack install/uninstall hooks
        Velopack.VelopackApp.Build().Run();

        using var mutex = new Mutex(true,
            @"Local\ClaudeStatusBar-9F4C2A77-2C5E-4E2A-9E3A-CSB", out bool createdNew);
        if (!createdNew) return;

        // Fire-and-forget non-fatal update check; only the singleton instance polls
        _ = CheckForUpdatesAsync();

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext(RendererSelector.Create));
        GC.KeepAlive(mutex);
    }

    private static async Task CheckForUpdatesAsync()
    {
        try
        {
            var mgr = new Velopack.UpdateManager(
                new Velopack.Sources.GithubSource(UpdateUrl, null, false));
            var info = await mgr.CheckForUpdatesAsync().ConfigureAwait(false);
            if (info != null)
            {
                await mgr.DownloadUpdatesAsync(info).ConfigureAwait(false);
                // Apply when the app next exits (it self-quits when no session is
                // active), without restarting now — the next SessionStart launches
                // the updated version. Avoids yanking the widget mid-session.
                mgr.WaitExitThenApplyUpdates(info, silent: true, restart: false);
            }
        }
        catch (Exception)
        {
            // Non-fatal: placeholder URL, no network, or not installed via Velopack — ignored
        }
    }
}
