namespace D2RLootRadar.Domain.Loot;

/// <summary>
/// Represents a successfully recognized item label.
/// </summary>
/// <param name="RawText">Original OCR output.</param>
/// <param name="NormalizedText">Cleaned text used for matching.</param>
/// <param name="Confidence">OCR confidence score between 0 and 1.</param>
/// <param name="BoundingBox">
/// Pixel position of the label within the original captured frame (pre-crop, pre-upscale).
/// Used to place the overlay marker.
/// </param>
/// <param name="Rarity">
/// Whether the label's color is Unique (tan/gold) or not.
/// <see cref="LabelRarity.Unknown"/> when no color could be sampled at all -
/// never treat Unknown as equivalent to <see cref="LabelRarity.Unique"/> or <see cref="LabelRarity.Other"/>.
/// </param>
public sealed record DetectionResult(
  string RawText,
  string NormalizedText,
  float Confidence,
  PixelRect BoundingBox,
  LabelRarity Rarity
);
