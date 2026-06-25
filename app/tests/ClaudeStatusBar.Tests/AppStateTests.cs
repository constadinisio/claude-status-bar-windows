using ClaudeStatusBar.Core;
using Xunit;

public class AppStateTests
{
    [Theory]
    [InlineData("idle", StatusKind.Idle)]
    [InlineData("thinking", StatusKind.Thinking)]
    [InlineData("tool", StatusKind.Tool)]
    [InlineData("permission", StatusKind.Permission)]
    [InlineData("done", StatusKind.Done)]
    [InlineData("DONE", StatusKind.Done)]   // case-insensitive
    [InlineData("", StatusKind.Idle)]       // vacío -> idle
    [InlineData(null, StatusKind.Idle)]     // null -> idle
    [InlineData("garbage", StatusKind.Idle)]// desconocido -> idle
    public void Parse_maps_raw_state_to_kind(string? raw, StatusKind expected)
        => Assert.Equal(expected, StatusKindParser.Parse(raw));
}
