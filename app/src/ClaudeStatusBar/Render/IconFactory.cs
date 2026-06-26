using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ClaudeStatusBar.Core;
using ClaudeStatusBar.Interop;
using ClaudeStatusBar.Render.Assets;

namespace ClaudeStatusBar.Render;

public static class IconFactory
{
    // Resting tints for the alpha-mask Claude logo.
    private static readonly Color RestColor = Color.FromArgb(0xD9, 0x77, 0x57); // Claude orange
    private static readonly Color PermissionColor = Color.Gold;

    // Process-lifetime caches of decoded/scaled bitmaps, keyed by pixel size.
    // The set is tiny and fixed, so caching for the app lifetime is cheap and
    // keeps Render() allocation-free (it only re-wraps cached bitmaps as HICONs).
    private static readonly object _lock = new();
    private static readonly Dictionary<int, Bitmap[]> _crabCache = new();
    private static readonly Dictionary<(int size, int argb), Bitmap> _logoCache = new();
    private static readonly Dictionary<(StatusKind, int), Bitmap[]> _fallbackCache = new();

    // Bitmaps for the given state — owned by the factory; callers must NOT dispose them.
    // Busy states animate (the crab); resting states return a single frame (the logo).
    public static Bitmap[] BitmapsFor(StatusKind kind, int sizePx)
    {
        try
        {
            return kind switch
            {
                StatusKind.Thinking or StatusKind.Tool => CrabBitmaps(sizePx),
                StatusKind.Permission                  => new[] { LogoBitmap(sizePx, PermissionColor) },
                _                                      => new[] { LogoBitmap(sizePx, RestColor) },
            };
        }
        catch
        {
            // Any decode/GDI failure falls back to the geometric dot — never crash the UI.
            return FallbackBitmaps(kind, sizePx);
        }
    }

    // Icons for the tray (NotifyIcon). Each call wraps the cached bitmaps in fresh
    // HICONs; the caller owns and disposes the returned Icons.
    public static Icon[] FramesFor(StatusKind kind, int sizePx)
    {
        var bmps = BitmapsFor(kind, sizePx);
        var icons = new Icon[bmps.Length];
        for (int i = 0; i < bmps.Length; i++)
            icons[i] = FromBitmap(bmps[i]);
        return icons;
    }

    private static Bitmap[] CrabBitmaps(int sizePx)
    {
        lock (_lock)
        {
            if (_crabCache.TryGetValue(sizePx, out var cached)) return cached;
            var frames = new Bitmap[ClawdFrames.Base64.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                using var raw = FrameDecoder.Decode(ClawdFrames.Base64[i]);
                frames[i] = FrameDecoder.Fit(raw, sizePx);
            }
            _crabCache[sizePx] = frames;
            return frames;
        }
    }

    private static Bitmap LogoBitmap(int sizePx, Color color)
    {
        lock (_lock)
        {
            var key = (sizePx, color.ToArgb());
            if (_logoCache.TryGetValue(key, out var cached)) return cached;
            using var raw = FrameDecoder.Decode(ClaudeLogo.Base64);
            using var tinted = FrameDecoder.Tint(raw, color);
            var fitted = FrameDecoder.Fit(tinted, sizePx);
            _logoCache[key] = fitted;
            return fitted;
        }
    }

    // Geometric-dot fallback (the original placeholder): a pulse for busy states,
    // a single coloured dot at rest. Cached like the real art.
    private static Bitmap[] FallbackBitmaps(StatusKind kind, int sizePx)
    {
        lock (_lock)
        {
            if (_fallbackCache.TryGetValue((kind, sizePx), out var cached)) return cached;
            var color = kind switch
            {
                StatusKind.Permission => Color.Gold,
                StatusKind.Done       => Color.LimeGreen,
                StatusKind.Idle       => Color.Gray,
                _                     => RestColor,
            };
            Bitmap[] frames;
            if (kind is StatusKind.Idle or StatusKind.Done or StatusKind.Permission)
                frames = new[] { DotBitmap(sizePx, color, 2) };
            else
                frames = new[] { DotBitmap(sizePx, color, 2), DotBitmap(sizePx, color, 3),
                                 DotBitmap(sizePx, color, 4), DotBitmap(sizePx, color, 5) };
            _fallbackCache[(kind, sizePx)] = frames;
            return frames;
        }
    }

    private static Bitmap DotBitmap(int sizePx, Color color, int inset)
    {
        var bmp = new Bitmap(sizePx, sizePx, PixelFormat.Format32bppArgb);
        try
        {
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, inset, inset, sizePx - inset * 2, sizePx - inset * 2);
            return bmp;
        }
        catch { bmp.Dispose(); throw; }
    }

    // Wraps a bitmap as a managed Icon, releasing the native HICON immediately.
    // Does not take ownership of `bmp` (it may be a long-lived cached bitmap).
    private static Icon FromBitmap(Bitmap bmp)
    {
        IntPtr h = bmp.GetHicon();
        try { using var tmp = Icon.FromHandle(h); return (Icon)tmp.Clone(); }
        finally { Native.DestroyIcon(h); }
    }
}
