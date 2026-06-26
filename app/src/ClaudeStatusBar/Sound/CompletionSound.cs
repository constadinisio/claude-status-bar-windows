using System.IO;
using System.Reflection;
using ClaudeStatusBar.Interop;

namespace ClaudeStatusBar.Sound;

// Plays the embedded completion.mp3 via MCI. The resource is extracted to a
// temp file once; each Play re-opens and plays it (turns are infrequent, so the
// open cost is irrelevant) at ~70% volume. Every failure is swallowed — audio
// is a nicety and must never affect the UI.
public sealed class CompletionSound : ICompletionSound, IDisposable
{
    private const string Alias = "claudeChime";
    private const string ResourceName = "ClaudeStatusBar.Assets.completion.mp3";

    private readonly string? _path;
    private bool _opened;

    public CompletionSound()
    {
        try { _path = Extract(); }
        catch { _path = null; }
    }

    public void Play()
    {
        if (_path is null) return;
        try
        {
            Mci($"close {Alias}");                                  // no-op if not open
            if (Mci($"open \"{_path}\" type mpegvideo alias {Alias}") != 0) return;
            _opened = true;
            Mci($"setaudio {Alias} volume to 700");                 // 0–1000
            Mci($"play {Alias} from 0");
        }
        catch { /* audio failure is non-fatal */ }
    }

    private static string Extract()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ClaudeStatusBar");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "completion.mp3");
        using var src = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new FileNotFoundException(ResourceName);
        using var dst = File.Create(path);
        src.CopyTo(dst);
        return path;
    }

    private static int Mci(string command) =>
        Native.mciSendString(command, null, 0, IntPtr.Zero);

    public void Dispose()
    {
        if (_opened) { try { Mci($"close {Alias}"); } catch { /* ignore */ } }
    }
}
