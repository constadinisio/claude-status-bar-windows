using System.Text.Json.Serialization;

namespace ClaudeStatusBar.Core;

// DTO 1:1 con el schema camelCase de state.json (state como string crudo).
internal sealed class StateDto
{
    public string? state { get; set; }
    public string? label { get; set; }
    public string? tool { get; set; }
    public string? project { get; set; }
    public string? sessionId { get; set; }
    public string? transcript { get; set; }
    public long startedAt { get; set; }
    public long ts { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(StateDto))]
internal partial class StateJsonContext : JsonSerializerContext { }
