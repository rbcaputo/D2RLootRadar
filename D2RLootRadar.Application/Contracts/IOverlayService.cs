namespace D2RLootRadar.Application.Contracts;

/// <summary>
/// Displays pulsing markers at the screen position of each just-detected watched item.
/// Never steals focus from the game.
/// </summary>
public interface IOverlayService
{
  /// <summary>
  /// Shows a marker at each given position for 5 seconds,
  /// or until the next detection - whichever comes first.
  /// No-ops if disabled.
  /// </summary>
  void ShowMarkers(IReadOnlyCollection<DetectionMarker> markers);

  /// <summary>
  /// Enables or disables the overlay.
  /// Disabling hides it immediately.
  /// </summary>
  void SetEnabled(bool enabled);

  /// <summary>
  /// Sets how long a marker stays visible before auto-hiding, in seconds.
  /// Takes effect on the next call to <see cref="ShowMarkers"/> -
  /// does not retroactively shorten or extend a marker already on screen.
  /// </summary>
  void SetMarkerDisplaySeconds(int seconds);
}
