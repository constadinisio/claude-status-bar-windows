using System.Drawing;
using System.Drawing.Drawing2D;
using ClaudeStatusBar.Core;
using ClaudeStatusBar.Interop;

namespace ClaudeStatusBar.Render;

public static class IconFactory
{
    // Creates a managed Icon from a Bitmap, releasing the native HICON immediately.
    private static Icon FromBitmap(Bitmap bmp)
    {
        IntPtr h = bmp.GetHicon();
        try { using var tmp = Icon.FromHandle(h); return (Icon)tmp.Clone(); }
        finally { Native.DestroyIcon(h); }
    }

    private static Bitmap NewCanvas(int size, out Graphics g)
    {
        var bmp = new Bitmap(size, size);
        g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        return bmp;
    }

    public static Icon Dot(int sizePx, Color color)
    {
        using var bmp = NewCanvas(sizePx, out var g);
        using (g)
        using (var brush = new SolidBrush(color))
            g.FillEllipse(brush, 2, 2, sizePx - 4, sizePx - 4);
        return FromBitmap(bmp);
    }

    // Placeholder animation: a dot that "pulses" across N frames.
    // Replaceable later with real art (spark/terminal/crab from the original project).
    public static Icon[] FramesFor(StatusKind kind, int sizePx)
    {
        var baseColor = kind switch
        {
            StatusKind.Permission => Color.Gold,
            StatusKind.Done       => Color.LimeGreen,
            StatusKind.Idle       => Color.Gray,
            _                     => Color.FromArgb(0xD9, 0x77, 0x57), // Claude orange
        };

        if (kind is StatusKind.Idle or StatusKind.Done or StatusKind.Permission)
            return new[] { Dot(sizePx, baseColor) };

        const int frames = 4;
        var result = new Icon[frames];
        for (int i = 0; i < frames; i++)
        {
            using var bmp = NewCanvas(sizePx, out var g);
            using (g)
            {
                int inset = 2 + i;                       // simple pulse animation
                using var brush = new SolidBrush(baseColor);
                g.FillEllipse(brush, inset, inset, sizePx - inset * 2, sizePx - inset * 2);
            }
            result[i] = FromBitmap(bmp);
        }
        return result;
    }
}
