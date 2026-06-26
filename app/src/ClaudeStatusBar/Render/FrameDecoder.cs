using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace ClaudeStatusBar.Render;

// Decodes base64-encoded PNG frames into GDI+ bitmaps, scales them to fit a
// square canvas, and tints alpha-mask art (the Claude logo) to a status color.
// Kept free of WPF types so it is unit-testable; the WPF widget converts the
// resulting Bitmaps to BitmapSource separately.
public static class FrameDecoder
{
    // base64 PNG -> independent Bitmap (no live stream dependency).
    // Throws FormatException on invalid base64 / image data.
    public static Bitmap Decode(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        using var ms = new MemoryStream(bytes);
        using var tmp = new Bitmap(ms);
        return new Bitmap(tmp);
    }

    // Scales src into a transparent size×size canvas, preserving aspect ratio
    // and centering. Nearest-neighbour keeps pixel art crisp.
    public static Bitmap Fit(Bitmap src, int size)
    {
        var canvas = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        try
        {
            using var g = Graphics.FromImage(canvas);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.Clear(Color.Transparent);

            float scale = Math.Min((float)size / src.Width, (float)size / src.Height);
            int w = Math.Max(1, (int)Math.Round(src.Width * scale));
            int h = Math.Max(1, (int)Math.Round(src.Height * scale));
            int x = (size - w) / 2;
            int y = (size - h) / 2;
            g.DrawImage(src, x, y, w, h);
            return canvas;
        }
        catch { canvas.Dispose(); throw; }
    }

    // Replaces RGB with `color` while preserving the mask's per-pixel alpha
    // (further scaled by color.A). Used to recolor the alpha-mask Claude logo.
    public static Bitmap Tint(Bitmap mask, Color color)
    {
        var outBmp = new Bitmap(mask.Width, mask.Height, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < mask.Height; y++)
                for (int x = 0; x < mask.Width; x++)
                {
                    int a = mask.GetPixel(x, y).A * color.A / 255;
                    outBmp.SetPixel(x, y, Color.FromArgb(a, color.R, color.G, color.B));
                }
            return outBmp;
        }
        catch { outBmp.Dispose(); throw; }
    }
}
