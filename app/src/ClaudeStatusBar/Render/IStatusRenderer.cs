using ClaudeStatusBar.Core;

namespace ClaudeStatusBar.Render;

public interface IStatusRenderer : IDisposable
{
    void Render(AppState state);
    event EventHandler? ExitRequested;
    event EventHandler? EmbedLost;
}
