using D2RLootRadar.Desktop.Interop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace D2RLootRadar.Desktop.Views;

/// <summary>
/// A full-virtual-desktop, click-through, always-non-activating window that renders a
/// pulsing ring at each detected item's screen position.
/// See <see cref="WindowInterop.ApplyOverlayStyles"/> for how it's kept from ever
/// stealing focus or appearing in Alt+Tab.
/// </summary>
public partial class OverlayWindow : Window
{
  /// <summary>
  /// Horizontal DPI scale factor, captured once at window creation,
  /// used to convert physical capture pixels to WPF device-independent units.
  /// </summary>
  public double DpiScaleX { get; private set; } = 1.0;

  /// <summary>
  /// Vertical DPI scale factor.
  /// See <see cref="DpiScaleX"/>.
  /// </summary>
  public double DpiScaleY { get; private set; } = 1.0;

  public OverlayWindow()
  {
    InitializeComponent();

    // Cover the full virtual desktop so absolute screen coordinates map directly onto the canvas.
    Left = SystemParameters.VirtualScreenLeft;
    Top = SystemParameters.VirtualScreenTop;
    Width = SystemParameters.VirtualScreenWidth;
    Height = SystemParameters.VirtualScreenHeight;

    SourceInitialized += (_, _) =>
    {
      IntPtr hwnd = new WindowInteropHelper(this).Handle;
      WindowInterop.ApplyOverlayStyles(hwnd);
    };
  }

  /// <summary>
  /// Captures the monitor's DPI scale as soon as the window handle exists,
  /// before any marker can be shown.
  /// </summary>
  protected override void OnSourceInitialized(EventArgs ea)
  {
    base.OnSourceInitialized(ea);

    // Capture scaling now - capture coordinates are physical pixels,
    // but WPF positions everything in device-independent units.
    DpiScale dpi = VisualTreeHelper.GetDpi(this);
    DpiScaleX = dpi.DpiScaleX;
    DpiScaleY = dpi.DpiScaleY;
  }

  /// <summary>
  /// Replaces any existing markers with a fresh pulsing ring at each given device-independent-unit point.
  /// </summary>
  /// <param name="points"></param>
  public void SetMarkers(IEnumerable<(double X, double Y)> points)
  {
    MarkersCanvas.Children.Clear();


    foreach (var (x, y) in points)
      MarkersCanvas.Children.Add(CreatePulsingRing(x, y));
  }

  /// <summary>
  /// Removes all currently-shown markers.
  /// </summary>
  public void ClearMarkers()
    => MarkersCanvas.Children.Clear();

  /// <summary>
  /// Builds a single ring that continuously scales outward (1x → 2.2x) while fading out,
  /// looping forever until removed by <see cref="ClearMarkers"/>.
  /// </summary>
  /// <param name="centerX"></param>
  /// <param name="centerY"></param>
  /// <returns></returns>
  private static UIElement CreatePulsingRing(double centerX, double centerY)
  {
    const double baseSize = 28;

    Ellipse ring = new()
    {
      Width = baseSize,
      Height = baseSize,
      Stroke = new SolidColorBrush(Color.FromRgb(255, 210, 60)),
      StrokeThickness = 3,
      RenderTransformOrigin = new(0.5, 0.5),
      RenderTransform = new ScaleTransform(1, 1)
    };

    Canvas.SetLeft(ring, centerX - baseSize / 2);
    Canvas.SetTop(ring, centerY - baseSize / 2);

    DoubleAnimation scaleAnimation = new()
    {
      From = 1.0,
      To = 2.2,
      Duration = TimeSpan.FromMilliseconds(900),
      RepeatBehavior = RepeatBehavior.Forever,
      EasingFunction = new QuadraticEase()
      {
        EasingMode = EasingMode.EaseOut
      }
    };
    DoubleAnimation opacityAnimation = new()
    {
      From = 1.0,
      To = 0.0,
      Duration = TimeSpan.FromMilliseconds(900),
      RepeatBehavior = RepeatBehavior.Forever
    };

    ScaleTransform transform = (ScaleTransform)ring.RenderTransform;
    transform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
    transform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
    ring.BeginAnimation(OpacityProperty, opacityAnimation);

    return ring;
  }
}
