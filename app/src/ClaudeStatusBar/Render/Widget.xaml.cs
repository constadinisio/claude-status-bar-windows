// app/src/ClaudeStatusBar/Render/Widget.xaml.cs
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClaudeStatusBar.App;
using ClaudeStatusBar.Core;

namespace ClaudeStatusBar.Render;

public partial class Widget : Window
{
    private const int IconSizePx = 30;
    private const int FrameMs = 90;

    // Converted frames are cached per state for the app lifetime (small, fixed set),
    // so switching states never re-decodes the PNGs.
    private static readonly Dictionary<StatusKind, BitmapSource[]> _frameCache = new();

    private readonly DispatcherTimer _anim;
    private BitmapSource[] _frames = System.Array.Empty<BitmapSource>();
    private int _frameIdx;
    private StatusKind? _currentKind;
    private AppState _lastState = AppState.Idle;

    public Widget()
    {
        InitializeComponent();
        _anim = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(FrameMs),
        };
        _anim.Tick += (_, _) =>
        {
            if (_frames.Length == 0) return;
            _frameIdx = (_frameIdx + 1) % _frames.Length;
            FrameImage.Source = _frames[_frameIdx];
            // Keep the elapsed clock ticking live between hook events (the poller
            // only delivers on state changes, so Update() alone would freeze it).
            ElapsedText.Text = StatusViewModel.Elapsed(_lastState, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        };
        // Stop the timer when the window closes (e.g. EmbedLost → swap to tray),
        // so it can't tick against a disposed widget.
        Closed += (_, _) => _anim.Stop();

        // Right-click toggle for the completion sound (the embedded widget's only menu).
        SoundMenuItem.Click += (_, _) => SoundSetting.Set(!SoundSetting.IsEnabled);
        if (SoundMenuItem.Parent is System.Windows.Controls.ContextMenu cm)
            cm.Opened += (_, _) => SoundMenuItem.IsChecked = SoundSetting.IsEnabled;
    }

    public IntPtr Handle => new WindowInteropHelper(this).EnsureHandle();

    public void Update(AppState s)
    {
        _lastState = s;
        LabelText.Text = StatusViewModel.ShortLabel(s);
        ElapsedText.Text = StatusViewModel.Elapsed(s, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        // Only rebuild the animation when the kind changes; otherwise just the
        // label/elapsed updated above. The crab keeps walking on its own timer.
        if (_currentKind == s.State) return;
        _currentKind = s.State;

        _frames = FramesFor(s.State);
        _frameIdx = 0;
        FrameImage.Source = _frames.Length > 0 ? _frames[0] : null;

        _anim.Stop();
        if (_frames.Length > 1) _anim.Start();
    }

    private static BitmapSource[] FramesFor(StatusKind kind)
    {
        if (_frameCache.TryGetValue(kind, out var cached)) return cached;
        var bmps = IconFactory.BitmapsFor(kind, IconSizePx);
        var srcs = new BitmapSource[bmps.Length];
        for (int i = 0; i < bmps.Length; i++) srcs[i] = ToBitmapSource(bmps[i]);
        _frameCache[kind] = srcs;
        return srcs;
    }

    // Encode to a PNG stream then load a frozen BitmapImage — copies the pixels so
    // no GDI handle is held (unlike CreateBitmapSourceFromHBitmap).
    private static BitmapSource ToBitmapSource(System.Drawing.Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Position = 0;
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = ms;
        img.EndInit();
        img.Freeze();
        return img;
    }
}
