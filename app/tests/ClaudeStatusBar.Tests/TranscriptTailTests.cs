using System.IO;
using ClaudeStatusBar.Core;
using Xunit;

public class TranscriptTailTests
{
    private static string TempFile(string content)
    {
        var p = Path.Combine(Path.GetTempPath(), "tt_" + Path.GetRandomFileName());
        File.WriteAllText(p, content);
        return p;
    }

    [Fact]
    public void Returns_last_non_empty_line_ignoring_trailing_newline()
    {
        var p = TempFile("line1\nline2\nlast line\n");
        try { Assert.Equal("last line", TranscriptTail.LastLine(p)); }
        finally { File.Delete(p); }
    }

    [Fact]
    public void Returns_the_only_line_when_no_trailing_newline()
    {
        var p = TempFile("only line");
        try { Assert.Equal("only line", TranscriptTail.LastLine(p)); }
        finally { File.Delete(p); }
    }

    [Fact]
    public void Missing_file_returns_null()
        => Assert.Null(TranscriptTail.LastLine(Path.Combine(Path.GetTempPath(), "does-not-exist-xyz.jsonl")));
}
