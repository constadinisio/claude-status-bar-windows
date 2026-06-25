// app/src/ClaudeStatusBar/Render/RendererSelector.cs
namespace ClaudeStatusBar.Render;

public static class RendererSelector
{
    public static IStatusRenderer Create()
    {
        EmbeddedRenderer? embedded = null;
        try
        {
            embedded = new EmbeddedRenderer();
            if (embedded.Embed()) return embedded;
        }
        catch { /* fall through to tray */ }
        embedded?.Dispose();
        return new TrayRenderer();
    }
}
