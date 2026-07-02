using D2RLootRadar.Domain.Loot;

namespace D2RLootRadar.Application.Contracts;

/// <summary>
/// Performs OCR against a captured frame.
/// </summary>
public interface IOcrService
{
  /// <summary>
  /// Detects all text tokens in a captured frame.
  /// Returns an empty collection (never null) when no text is found or <paramref name="imageData"/> is empty.
  /// </summary>
  /// <param name="imageData">A PNG-encoded frame, as produced by <see cref="IGameCaptureService"/>.</param>
  /// <param name="cToken">Cancels the operation.</param>
  /// <returns></returns>
  Task<IReadOnlyCollection<DetectionResult>> DetectAsync(byte[] imageData, CancellationToken cToken);
}
