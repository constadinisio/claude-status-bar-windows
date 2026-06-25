// app/tests/ClaudeStatusBar.Tests/StatusBarPathsTests.cs
using ClaudeStatusBar.Core;
using Xunit;

public class StatusBarPathsTests
{
    [Fact]
    public void StateFile_lives_under_user_profile_dot_claude_statusbar()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expected = Path.Combine(profile, ".claude", "statusbar", "state.json");
        Assert.Equal(expected, StatusBarPaths.StateFile);
    }

    [Fact]
    public void SessionsDir_ends_with_sessions_d()
        => Assert.EndsWith(Path.Combine("statusbar", "sessions.d"), StatusBarPaths.SessionsDir);
}
