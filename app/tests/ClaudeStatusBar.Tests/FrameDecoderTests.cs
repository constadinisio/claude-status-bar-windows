using System.Drawing;
using System.Drawing.Imaging;
using ClaudeStatusBar.Render;
using Xunit;

public class FrameDecoderTests
{
    // 1x1 opaque PNG (red pixel), valid base64.
    private const string OnePxPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwAEhgGAWjR9awAAAABJRU5ErkJggg==";

    [Fact]
    public void Decode_validBase64_returnsBitmap()
    {
        using var bmp = FrameDecoder.Decode(OnePxPng);
        Assert.NotNull(bmp);
        Assert.Equal(1, bmp.Width);
        Assert.Equal(1, bmp.Height);
    }

    [Fact]
    public void Decode_invalidBase64_throws()
    {
        Assert.ThrowsAny<Exception>(() => FrameDecoder.Decode("not valid base64 @@@"));
    }

    [Fact]
    public void Fit_producesSquareOfRequestedSize()
    {
        using var src = new Bitmap(8, 4, PixelFormat.Format32bppArgb);
        using var fitted = FrameDecoder.Fit(src, 16);
        Assert.Equal(16, fitted.Width);
        Assert.Equal(16, fitted.Height);
    }

    [Fact]
    public void Tint_replacesRgbAndPreservesAlpha()
    {
        using var mask = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
        mask.SetPixel(0, 0, Color.FromArgb(255, 0, 0, 0)); // opaque
        mask.SetPixel(1, 1, Color.FromArgb(0, 0, 0, 0));   // transparent

        using var tinted = FrameDecoder.Tint(mask, Color.FromArgb(255, 217, 119, 87));

        var opaque = tinted.GetPixel(0, 0);
        Assert.Equal(255, opaque.A);
        Assert.Equal(217, opaque.R);
        Assert.Equal(119, opaque.G);
        Assert.Equal(87, opaque.B);

        var transparent = tinted.GetPixel(1, 1);
        Assert.Equal(0, transparent.A);
    }
}
