using System.Diagnostics;
using ClaudeStatusBar.Core;
using ClaudeStatusBar.Render;
using Xunit;

public class IconFactoryTests
{
    [Fact]
    public void FramesFor_returns_at_least_one_frame()
        => Assert.NotEmpty(IconFactory.FramesFor(StatusKind.Thinking, 16));

    [Theory]
    [InlineData(StatusKind.Thinking)]
    [InlineData(StatusKind.Tool)]
    public void FramesFor_busy_states_return_the_twenty_crab_frames(StatusKind kind)
        => Assert.Equal(20, IconFactory.FramesFor(kind, 16).Length);

    [Theory]
    [InlineData(StatusKind.Idle)]
    [InlineData(StatusKind.Done)]
    [InlineData(StatusKind.Permission)]
    public void FramesFor_resting_states_return_a_single_frame(StatusKind kind)
        => Assert.Single(IconFactory.FramesFor(kind, 16));

    [Fact]
    public void Generating_many_icons_does_not_leak_gdi_handles()
    {
        var proc = Process.GetCurrentProcess();
        proc.Refresh();
        long before = proc.HandleCount;
        for (int i = 0; i < 500; i++)
            foreach (var f in IconFactory.FramesFor(StatusKind.Tool, 16)) f.Dispose();
        proc.Refresh();
        // Margen amplio; si hubiera fuga de HICON, crecería ~500+.
        Assert.True(proc.HandleCount - before < 200,
            $"posible fuga de handles GDI: +{proc.HandleCount - before}");
    }
}
