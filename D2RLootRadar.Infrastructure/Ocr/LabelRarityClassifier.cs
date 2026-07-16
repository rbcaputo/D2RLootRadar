using D2RLootRadar.Domain.Loot;

namespace D2RLootRadar.Infrastructure.Ocr;

/// <summary>
/// Classifies a sampled label color into the D2R item-quality tier it represents.
/// 
/// <para>
/// Calibrated against clear in-game captures of all six label colors,
/// later validated against real gameplay debug logs.
/// </para>
/// 
/// <para>
/// Saturation is the primary discriminator:
/// white and gray both collapse to near-zero saturation regardless of hue, splitting instead on brightness.
/// Unique and Rare are the hardest pair - both sit in the same yellow/gold hue family, only ~15 degrees apart -
/// but they separate cleanly on saturation and value
/// (Unique: ~25%^saturation / ~78% value; Rare: ~50% saturation / ~95% value).
/// Rune/Material (orange) lives in that same hue family too, but at ~98% saturation -
/// comfortably clear of Rare's ~50%, so it splits off the same way Unique/Rare do, just one tier further out.
/// </para>
/// </summary>
public static class LabelRarityClassifier
{
  /// <summary>
  /// Saturation below this is achromatic (white/gray) - splits on brightness instead of hue.
  /// </summary>
  private const double AchromaticSaturationCeiling = 0.10;

  /// <summary>
  /// Value above which an achromatic sample is Normal (white) rather than EtherealSocketed (gray).
  /// </summary>
  private const double NormalValueFloor = 0.80;

  /// <summary>
  /// Saturation below this, within the gold/yellow hue band, is Unique rather than Rare.
  /// </summary>
  private const double UniqueSaturationCeiling = 0.38;

  /// <summary>
  /// Saturation above this, within the yellow/gold hue band, is Rune/Material (orange) rather than Rare -
  /// measured ~98% against Rare's ~50%, so this sits with plenty of margin either side.
  /// </summary>
  private const double RuneMaterialSaturationFloor = 0.75;

  /// <summary>
  /// Shard (red) sits near hue 0 and wraps across it - anything at or below this hue,
  /// or at or above <see cref="ShardHueFloor"/>, is in band.
  /// </summary>
  private const double ShardHueCeiling = 20;

  /// <summary>
  /// <see cref="ShardHueCeiling"/>.
  /// </summary>
  private const double ShardHueFloor = 340;

  /// <summary>
  /// Classifies a single sampled label color.
  /// </summary>
  public static LabelRarity Classify(byte r, byte g, byte b)
  {
    (double hue, double saturation, double value) = RgbToHsv(r, g, b);

    // Achromatic: white vs. gray splits on brightness alone.
    if (saturation < AchromaticSaturationCeiling)
      return value > NormalValueFloor
        ? LabelRarity.Normal
        : LabelRarity.EtherealSocketed;

    // Red wraps across 0 degress, so it's checked before the other distinct hue families.
    if (hue <= ShardHueCeiling || hue >= ShardHueFloor)
      return LabelRarity.Shard;

    // Distinct hue families - low ambiguity.
    if (hue >= 200 && hue <= 260)
      return LabelRarity.Magic;
    if (hue >= 90 && hue <= 160)
      return LabelRarity.Set;

    // Gold/yellow family: Rune/Material, Unique and Rare overlap in hue, separate on saturation instead.
    if (hue >= 35 && hue <= 70)
    {
      if (saturation >= RuneMaterialSaturationFloor)
        return LabelRarity.RuneMaterial;

      return saturation < UniqueSaturationCeiling
        ? LabelRarity.Unique
        : LabelRarity.Rare;
    }

    // Any other hue isn't a color D2R renders item labels in.
    return LabelRarity.Unknown;
  }

  private static (double Hue, double Saturation, double Value) RgbToHsv(byte r, byte g, byte b)
  {
    double rd = r / 255.0, gd = g / 255.0, bd = b / 255.0;
    double max = Math.Max(rd, Math.Max(gd, bd));
    double min = Math.Min(rd, Math.Min(gd, bd));
    double delta = max - min;

    double hue = 0;
    if (delta > 0.00001)
    {
      if (max == rd)
        hue = 60 * (((gd - bd) / delta) % 6);
      else if (max == gd)
        hue = 60 * (((bd - rd) / delta) + 2);
      else
        hue = 60 * (((rd - gd) / delta) + 4);
    }

    if (hue < 0)
      hue += 360;

    double saturation = max <= 0 ? 0 : delta / max;
    double value = max;

    return (hue, saturation, value);
  }
}
