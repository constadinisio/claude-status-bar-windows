// app/src/ClaudeStatusBar/Render/RendererSelector.cs
namespace ClaudeStatusBar.Render;

public static class RendererSelector
{
    public static IStatusRenderer Create()
    {
        try
        {
            var embedded = new EmbeddedRenderer();
            if (embedded.Embed()) return embedded;
            embedded.Dispose();
        }
        catch { /* fall through to tray */ }
        return new TrayRenderer();
    }
}
