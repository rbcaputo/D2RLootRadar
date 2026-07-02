namespace D2RLootRadar.Application.Contracts;

/// <summary>
/// Monitors global keyboard activity.
/// 
/// The Infrastructure layer is responsible for implementing the low-level hook.
/// </summary>
public interface IKeyboardMonitor : IDisposable
{
  /// <summary>
  /// Raised when the Left ALT key is pressed.
  /// </summary>
  event EventHandler? AltPressed;

  /// <summary>
  /// Raised when the Left ALT key is released.
  /// </summary>
  event EventHandler? AltReleased;

  /// <summary>
  /// Starts monitoring.
  /// </summary>
  void Start();

  /// <summary>
  /// Stops monitoring.
  /// </summary>
  void Stop();
}
