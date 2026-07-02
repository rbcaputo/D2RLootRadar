using D2RLootBeeper.Desktop.Views;
using D2RLootRadar.Application.Contracts;
using System.Windows.Interop;
using System.Windows.Threading;

namespace D2RLootRadar.Desktop.Services;

/// <summary>
/// Owns the overlay window's lifetime and marshals calls onto the UI thread,
/// since detections derive from a background thread-pool thread inside
/// <see cref="Application.Monitoring.LootMonitoringService"/>.
/// </summary>
public sealed class OverlayService : IOverlayService
{
  private readonly Dispatcher _dispatcher;
  private readonly OverlayWindow _window;
  private readonly DispatcherTimer _hideTimer;

  private bool _enabled;

  /// <summary>
  /// Creates the (initially hidden) overlay window and seeds <see cref="_enabled"/>
  /// from persisted settings.
  /// </summary>
  /// <param name="settingsStore"></param>
  public OverlayService(ISettingsStore settingsStore)
  {
    _dispatcher = System.Windows.Application.Current.Dispatcher;
    _window = new();

    // Force the window handle to exist immediately so DPI scaling is known before the very first marker is shown -
    // whithout this, the first detection after startup would use the wrong scale.
    new WindowInteropHelper(_window).EnsureHandle();

    _hideTimer = new()
    {
      Interval = TimeSpan.FromSeconds(5)
    };
    _hideTimer.Tick += (_, _) =>
    {
      _hideTimer.Stop();
      _window.ClearMarkers();
      _window.Hide();
    };

    _enabled = settingsStore.Load().OverlayEnabled;
  }

  /// <inheritdoc />
  public void ShowMarkers(IReadOnlyCollection<DetectionMarker> markers)
  {
    if (!_enabled || markers.Count == 0)
      return;

    // Detections arrive from a background thread - marshal to the UI thread.
    _dispatcher.BeginInvoke(() =>
    {
      _hideTimer.Stop();

      if (!_window.IsVisible)
        _window.Show();

      var points = markers.Select(m =>
        (X: m.ScreenX / _window.DpiScaleX, Y: m.ScreenY / _window.DpiScaleY)
      );

      _window.SetMarkers(points);
      _hideTimer.Start(); // restarts the 5 s window on every new detection
    });
  }

  /// <inheritdoc />
  public void SetEnabled(bool enabled)
  {
    _enabled = enabled;
    if (!enabled)
      _dispatcher.BeginInvoke(() =>
      {
        _hideTimer.Stop();
        _window.ClearMarkers();
        _window.Hide();
      });
  }
}
