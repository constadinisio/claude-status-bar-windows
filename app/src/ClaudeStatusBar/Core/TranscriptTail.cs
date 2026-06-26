using System.IO;
using System.Text;

namespace ClaudeStatusBar.Core;

// Reads the last non-empty line of a (potentially large) transcript .jsonl by
// reading only the tail of the file. Returns null if unreadable.
public static class TranscriptTail
{
    private const int TailBytes = 64 * 1024;

    public static string? LastLine(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            int toRead = (int)Math.Min(TailBytes, fs.Length);
            if (toRead == 0) return null;
            fs.Seek(-toRead, SeekOrigin.End);
            var buf = new byte[toRead];
            int read = fs.Read(buf, 0, toRead);
            var text = Encoding.UTF8.GetString(buf, 0, read);

            var lines = text.Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i].TrimEnd('\r');
                if (!string.IsNullOrWhiteSpace(line)) return line;
            }
            return null;
        }
        catch { return null; }
    }
}
