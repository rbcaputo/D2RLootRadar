namespace D2RLootRadar.Domain.Loot;

/// <summary>
/// A captured game frame plus the on-screen position of the window it was captured from.
/// WindowBounds lets downstream code translate frame-local pixel coordinates
/// (e.g. an OCR bounding box) into absolute screen coordinates for overlay placement.
/// </summary>
public sealed record CaptureFrame(byte[] ImageData, PixelRect WindowBounds)
{
  /// <summary>
  /// A sentinel representing "no frame captured" (e.g. D2R window not found), rather than null.
  /// </summary>
  public static readonly CaptureFrame Empty
    = new([], default);

  /// <summary>
  /// True if this is <see cref="Empty"/> or otherwise carries no image data.
  /// </summary>
  public bool IsEmpty
    => ImageData.Length == 0;
}
