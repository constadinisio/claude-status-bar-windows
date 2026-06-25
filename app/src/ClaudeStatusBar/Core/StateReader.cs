using System.IO;
using System.Text.Json;
using System.Threading;

namespace ClaudeStatusBar.Core;

public sealed class StateReader
{
    private readonly string _path;
    public StateReader(string path) => _path = path;

    public AppState? TryRead()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (!File.Exists(_path)) return null;
                using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read,
                                              FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                var json = sr.ReadToEnd();
                if (string.IsNullOrWhiteSpace(json)) return null;

                var dto = JsonSerializer.Deserialize(json, StateJsonContext.Default.StateDto);
                if (dto is null) return null;

                return new AppState(
                    StatusKindParser.Parse(dto.state),
                    dto.label ?? "", dto.tool ?? "", dto.project ?? "",
                    dto.sessionId ?? "", dto.transcript ?? "",
                    dto.startedAt, dto.ts);
            }
            catch (IOException) when (attempt < 2) { Thread.Sleep(20); }
            catch (IOException) { return null; }   // final attempt exhausted
            catch (JsonException) { return null; }
        }
        return null;
    }
}
