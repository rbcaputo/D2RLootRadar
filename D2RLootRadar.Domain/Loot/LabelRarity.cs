namespace D2RLootRadar.Domain.Loot;

/// <summary>
/// Represents whether a dropped item's label is a Unique (tan/gold) label or not.
/// 
/// <para>
/// Deliberately collapsed to three values rather than one per D2R quality tier.
/// The only decision the app ever needs to make is "does this label count toward the Unique-only filter" -
/// white, gray, blue, yellow, and green all behave identically (they're simply not Unique),
/// so giving them separate names would model detail the rest of the pipeline never consumes.
/// </para>
/// </summary>
public enum LabelRarity
{
  /// <summary>
  /// No color could be sampled for the label at all (e.g. the bounding box was empty or degenerate).
  /// Distinct from <see cref="Other"/> - this means "couldn't tell, not "confirmed not Unique".
  /// Callers must not treat this as a match.
  /// </summary>
  Unknowm,

  /// <summary>
  /// A color was sampled and it confidently did not martch the Unique tan/gold signature -
  /// covers white, gray, blue, yellow, and green labels alike.
  /// </summary>
  Other,

  /// <summary>
  /// Tan/gold label - Unique item.
  /// </summary>
  Unique
}
