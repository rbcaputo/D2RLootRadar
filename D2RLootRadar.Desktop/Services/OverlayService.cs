using D2RLootRadar.Desktop.Views;
using D2RLootRadar.Application.Contracts;
using System.Windows.Interop;
using System.Windows.Threading;
using D2RLootRadar.Desktop.Interop;
using D2RLootRadar.Infrastructure.Processes;

namespace D2RLootRadar.Desktop.Services;

/// <summary>
/// Owns the overlay window's lifetime and marshals calls onto the UI thread,
/// since detections derive from a background thread-pool thread inside
/// <see cref="Application.Monitoring.LootMonitoringService"/>.
/// 
/// <para>
/// Also hides the overlay the instant focus moves away from D2R,
/// via <see cref="ForegroundWindowWatcher"/> - the overlay window is <c>Topmost</c>,
/// a static Win32 z-order flag with no awereness of which window is currently focused,
/// so without this it would keep sitting on top of whatever window the user alt-tabbed to until the
/// display timer happened to expire.
/// </para>
/// </summary>
public sealed class OverlayService : IOverlayService, IDisposable
{
  private readonly Dispatcher _dispatcher;
  private readonly OverlayWindow _window;
  private readonly DispatcherTimer _hideTimer;
  private readonly ForegroundWindowWatcher _foregroundWatcher;

  private bool _enabled;

  /// <summary>
  /// Creates the (initially hidden) overlay window and seeds <see cref="_enabled"/>
  /// from persisted settings.
  /// </summary>
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

    // Installed here specifically because OverlayService is constructed on the WPF UI thread during startup -
    // see ForegroundWindowWatcher.Start()'s remarks on why the calling thread matters.
    _foregroundWatcher = new();
    _foregroundWatcher.ForegroundChanged += OnForegroundChanged;
    _foregroundWatcher.Start();

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

  /// <inheritdoc />
  public void SetMarkerDisplaySeconds(int seconds)
    =>
    // Mutating a DispatcherTimer's Interval must happen on the thread that owns it.
    // In practice this is always called from the UI thread already (Settings window binding),
    // but dispatching keeps the contract safe regardless of caller thread.
    _dispatcher.BeginInvoke(() =>
      _hideTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, seconds))
    );

  /// <summary>
  /// Hides the overlay the instant focus moves away from D2R,
  /// rgardless of how much time is left on <see cref="_hideTimer"/> -
  /// a marker should never linger on top of whatever window the user alt-tabbed to.
  /// 
  /// Already running on the UI thread - see <see cref="ForegroundWindowWatcher.ForegroundChanged"/> -
  /// so this touches <see cref="_window"/> directly rather than going through <see cref="_dispatcher"/>.
  /// </summary>
  /// <param name="hwnd"></param>
  private void OnForegroundChanged(IntPtr hwnd)
  {
    if (!_window.IsVisible || hwnd == GameWindowLocator.FindWindow())
      return; // nothing showing, or focus is still (or again) on D2R - nothing to do

    _hideTimer.Stop();
    _window.ClearMarkers();
    _window.Hide();
  }

  /// <summary>
  /// Unhooks <see cref="_foregroundWatcher"/>.
  /// The overlay window itself is a DI-owned resource with the app's own lifetime and is not disposed here.
  /// </summary>
  public void Dispose()
  {
    _foregroundWatcher.ForegroundChanged -= OnForegroundChanged;
    _foregroundWatcher.Dispose();
  }
}