using D2RLootRadar.Domain.Loot;

namespace D2RLootRadar.Application.Contracts;

/// <summary>
/// Captures the current D2R game frame.
/// </summary>
public interface IGameCaptureService
{
  /// <summary>
  /// Captures the current game frame along with the window's on-screen position,
  /// so callers can translate frame-local coordinates to absolute screen coordinates.
  /// </summary>
  Task<CaptureFrame> CaptureAsync(CancellationToken cToken);
}
