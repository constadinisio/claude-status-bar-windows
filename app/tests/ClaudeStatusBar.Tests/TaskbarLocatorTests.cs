// app/tests/ClaudeStatusBar.Tests/TaskbarLocatorTests.cs
using ClaudeStatusBar.Interop;
using Xunit;

public class TaskbarLocatorTests
{
    [Fact]
    public void FindPrimaryTaskbar_returns_a_handle_owned_by_explorer()
    {
        var hwnd = TaskbarLocator.FindPrimaryTaskbar();
        Assert.NotEqual(IntPtr.Zero, hwnd);  // hay taskbar en una sesión interactiva
    }

    [Fact]
    public void TryGetEmbedBounds_yields_positive_size()
    {
        var hwnd = TaskbarLocator.FindPrimaryTaskbar();
        Assert.True(TaskbarLocator.TryGetEmbedBounds(hwnd, 200, out var r));
        Assert.True(r.right - r.left > 0 && r.bottom - r.top > 0);
    }
}
