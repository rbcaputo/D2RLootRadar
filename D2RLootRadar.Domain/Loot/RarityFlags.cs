namespace D2RLootRadar.Domain.Loot;

/// <summary>
/// Which item qualities the user wants to be alerted for, per watched item base.
/// 
/// <para>
/// A <see cref="FlagsAttribute"/> enum because selections add up -
/// checking both <see cref="Magic"/> and <see cref="Rare"/> for an item means either one alerts.
/// </para>
/// 
/// <para>
/// <see cref="Superior"/> is not a label color - it shares gray with plain <see cref="EtherealSocketed"/> items -
/// so it's checked from the recognized text, independently of whichever color flags are also selected.
/// See <c>LootMonitoringService.IsRarityMatch</c> for how the two checks combine.
/// </para>
/// 
/// <para>
/// Member names match the strings used in <c>item-bases.json</c>'s <c>Qualities</c> array exactly,
/// so parsing is a direct <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/> per
/// entry with no separate name-mapping table to maintain -
/// <see cref="Rune"/> and <see cref="Shard"/> are the two exceptions:
/// Rune/Gem/Material entries omit <c>Qualities</c> entirely,
/// so those two are assigned directly by <c>JsonItemBaseCatalog</c> based on category rather than parsed from JSON text.
/// </para>
/// </summary>
[Flags]
public enum RarityFlags
{
  None = 0,

  /// <summary
  /// >White label.
  /// </summary>
  Normal = 1 << 0,

  /// <summary>
  /// Gray label - a Normal-quality item that is Ethereal and/or Socketed.
  /// </summary>
  EtherealSocketed = 1 << 1,

  /// <summary>
  /// Blue label.
  /// </summary>
  Magic = 1 << 2,

  /// <summary>
  /// Yellow label.
  /// </summary>
  Rare = 1 << 3,

  /// <summary>
  /// Green label.
  /// </summary>
  Set = 1 << 4,

  /// <summary>
  /// Tan/gold label.
  /// </summary>
  Unique = 1 << 5,

  /// <summary>
  /// Orange label - Runes, and every Material except Shards.
  /// Not a quality tier; these bases have no rarity variation at all,
  /// this is just their one fixed label color.
  /// </summary>
  RuneMaterial = 1 << 6,

  /// <summary>
  /// Red label - Worldstone Shards specifically.
  /// </summary>
  Shard = 1 << 7,

  /// <summary>
  /// Text prefix, not a color - only ever appears on <see cref="Normal"/> or <see cref="EtherealSocketed"/> labels.
  /// </summary>
  Superior = 1 << 8
}
