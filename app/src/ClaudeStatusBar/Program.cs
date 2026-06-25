using System.Windows.Forms;
using ClaudeStatusBar.App;
using ClaudeStatusBar.Render;

namespace ClaudeStatusBar;

internal static class Program
{
    // Placeholder — replace with real GitHub repo URL when published
    private const string UpdateUrl =
        "https://github.com/REPLACE_ME_OWNER/claude-status-bar-windows";

    [STAThread]
    private static void Main()
    {
        // MUST be the very first statement — handles Velopack install/uninstall hooks
        Velopack.VelopackApp.Build().Run();

        // Fire-and-forget non-fatal update check; never blocks or crashes startup
        _ = CheckForUpdatesAsync();

        using var mutex = new Mutex(true,
            @"Global\ClaudeStatusBar-9F4C2A77-2C5E-4E2A-9E3A-CSB", out bool createdNew);
        if (!createdNew) return;

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
                await mgr.DownloadUpdatesAsync(info).ConfigureAwait(false);
        }
        catch
        {
            // Non-fatal: placeholder URL, no network, or not installed via Velopack — ignored
        }
    }
}
