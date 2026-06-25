// app/tests/ClaudeStatusBar.Tests/StatusViewModelTests.cs
using ClaudeStatusBar.Core;
using Xunit;

public class StatusViewModelTests
{
    private static AppState S(StatusKind k, string label = "", long started = 0)
        => new(k, label, "", "proj", "s", "", started, 0);

    [Fact]
    public void Elapsed_formats_mm_ss()
        => Assert.Equal("00:05", StatusViewModel.Elapsed(S(StatusKind.Tool, started: 1000), 1005));

    [Fact]
    public void Elapsed_is_empty_when_not_started()
        => Assert.Equal("", StatusViewModel.Elapsed(S(StatusKind.Idle), 1005));

    [Fact]
    public void Permission_dot_only_when_permission()
    {
        Assert.True(StatusViewModel.ShowPermissionDot(S(StatusKind.Permission)));
        Assert.False(StatusViewModel.ShowPermissionDot(S(StatusKind.Thinking)));
    }

    [Fact]
    public void Tooltip_includes_project_and_label()
    {
        var t = StatusViewModel.Tooltip(S(StatusKind.Tool, "Running Bash", started: 1000));
        Assert.Contains("proj", t);
        Assert.Contains("Running Bash", t);
    }
}
