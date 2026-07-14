namespace D2RLootRadar.Domain.Loot;

/// <summary>
/// The item-quality tier classified from a dropped item's label color.
/// 
/// <para>
/// The full six-color breakdown needed to distinguish item rarity.
/// </para>
/// </summary>
public enum LabelRarity
{
  /// <summary>
  /// No color could be sampled at all (e.g. an empty bounding box),
  /// or the sampled color didn't fall into any recognized band.
  /// Callers must not treat this as a match for any <see cref="RarityFlags"/> value.
  /// </summary>
  Unknown,

  /// <summary>
  /// White label.
  /// </summary>
  Normal,

  /// <summary>
  /// Gray label - a Normal-quality item that is Ethereal and/or Socketed.
  /// </summary>
  EtherealSocketed,

  /// <summary>
  /// Blue label.
  /// </summary>
  Magic,

  /// <summary>
  /// Yellow label.
  /// </summary>
  Rare,

  /// <summary>
  /// Green label.
  /// </summary>
  Set,

  /// <summary>
  /// Tan/gold label.
  /// </summary>
  Unique
}
