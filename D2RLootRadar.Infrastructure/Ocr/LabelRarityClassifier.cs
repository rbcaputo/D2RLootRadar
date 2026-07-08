using D2RLootRadar.Domain.Loot;

namespace D2RLootRadar.Infrastructure.Ocr;

/// <summary>
/// Decides whether a sampled label color is Unique (tan/glod) or not.
/// 
/// <para>
/// Calibrated against clean in-game captures of all six D2R label colors, but the decision itself is binary -
/// the app only ever needs to know "is this the tan/gold Unique label."
/// White, gray, blue, yellow, and green are all simply "not Unique";
/// they're never distinguished from each other.
/// </para>
/// 
/// <para>
/// The one real difficulty is Rare vs. Unique: both sit in the same yellow/gold hue family,
/// only ~15 degrees apart. They are still separate cleanly on saturation and value
/// (Unique: ~25% saturation / ~78% value; Rare: ~50% saturation / ~95% value),
/// so saturation - not hue - is the deciding factor within that band.
/// </para>
/// 
/// <para>
/// Given a real RGB triple, this always returns a definite answer (never "unknown) -
/// pure and dependency-free by design so it can be unit-tested directly,
/// the same way <c>FuzzyMatcher</c> is tested without meeding a real captured frame.
/// The <see cref="LabelRarity.Unknown"/> case belongs to the called:
/// it means "no color could be sampled at all" (e.g. an empty bounding box),
/// which is a different failure mode than "sampled a color and it wasn't Unique".
/// </para>
/// </summary>
public static class LabelRarityClassifier
{
  /// <summary>
  /// Saturation below this is achromatic (white/gray) - never Unique regardless of hue.
  /// </summary>
  private const double AchromaticSaturationCeiling = 0.10;

  /// <summary>
  /// Saturation below this, within the gold/yellow hue band, is Unique rather than Rare.
  /// </summary>
  private const double UniqueSaturationCeiling = 0.38;

  /// <summary>
  /// Classifies a single sampled label color as Unique or Other.
  /// Never returns <see cref="LabelRarity.Unknowm"/> -
  /// that value is reserved for callers that couldn't sample a color in the first place.
  /// </summary>
  public static LabelRarity Classify(byte r, byte g, byte b)
  {
    (double hue, double saturation) = RgbToHsv(r, g, b);

    // Achromatic (white/gray): never Unique.
    if (saturation < AchromaticSaturationCeiling)
      return LabelRarity.Other;

    //Blue (magic) and Green (Set) hue families: never Unique.
    if (hue >= 200 && hue <= 260)
      return LabelRarity.Other;
    if (hue >= 90 && hue <= 160)
      return LabelRarity.Other;

    // Gold/yellow family: Unique and Rare overlap in hue, separate on saturation instead.
    if (hue >= 35 && hue <= 70)
      return saturation < UniqueSaturationCeiling
        ? LabelRarity.Unique
        : LabelRarity.Other;

    // Any other hue isn't a color D2R renders item labels in - definitely not Unique.
    return LabelRarity.Other;
  }

  private static (double Hue, double Saturation) RgbToHsv(byte r, byte g, byte b)
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

    return (hue, saturation);
  }
}
