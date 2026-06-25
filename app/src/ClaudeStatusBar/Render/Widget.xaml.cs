// app/src/ClaudeStatusBar/Render/Widget.xaml.cs
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ClaudeStatusBar.Core;

namespace ClaudeStatusBar.Render;

public partial class Widget : Window
{
    public Widget() => InitializeComponent();

    public IntPtr Handle => new WindowInteropHelper(this).EnsureHandle();

    public void Update(AppState s)
    {
        LabelText.Text = StatusViewModel.ShortLabel(s);
        ElapsedText.Text = StatusViewModel.Elapsed(s, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Dot.Fill = s.State switch
        {
            StatusKind.Permission => System.Windows.Media.Brushes.Gold,
            StatusKind.Done       => System.Windows.Media.Brushes.LimeGreen,
            StatusKind.Idle       => System.Windows.Media.Brushes.Gray,
            _                     => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD9, 0x77, 0x57)),
        };
    }
}
