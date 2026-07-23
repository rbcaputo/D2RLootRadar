namespace D2RLootRadar.Domain.Loot;

/// <summary>
/// Represents a D2R item base as it appears on a floor label.
/// </summary>
/// <param name="Name">
/// The exact text that appears on the floor label - the value the OCR pipeline matches against.
/// </param>
/// <param name="DisplayGroup">
/// Sub-classification derived from the item database taxonomy (Subtype ?? Type).
/// Drives the grouping in the config UI - e.g. "Axe", "Sword", "Circlet", "Rune", "Key".
/// </param>
/// <param name="Category">
/// Broad domain used for ordering in the UI and any future category-scoped logic.
/// </param>
/// <param name="Tier">
/// The item's power tier within its Type -
/// "Normal"/"Exceptional"/"Elite" for weapons and armor, a different vocabulary entirely for Rune
/// ("Low"/"Mid"/"High") and Gem ("Chipped"/"Flaed"/...), and null for bases with no tier system at all
/// (Ring, Amulet, Charm, Jewel).
/// Drives the main window's Tier filter - see <c>CategoryViewModel.ApplyFilters</c>.
/// That filter's three options are deliberately just Normal/Exceptional/Elite, so a base whose tier is null or
/// one of the Rune/Gem-specific values won't match any of them -
/// see the filter's own remarks for why that's the intended behavior, not a gap.
/// </param>
/// <param name="ApplicableRarities">
/// Which <see cref="RarityFlags"/> this specific base can actually appear as -
/// drives which rarity dots the watch-list UI's rarity picker even offers for this item.
/// Always at least one flag - bases with no quality variation (Rune, Gem, Material) still
/// resolve to whichever single flag matches their one fixed label color
/// (<see cref="RarityFlags.Normal"/>, <see cref="RarityFlags.RuneMaterial"/>, or <see cref="RarityFlags.Shard"/>)
/// rather than being left unselectable.
/// </param>
/// <param name="MaxSockets">
/// Maximum sockets this base can roll, shown in the watch-list UI's info tooltip.
/// Null when the base can't be socketed at all (e.g. Charms, most jewelry, etc.).
/// </param>
/// <param name="SetVariants">
/// Names of Set items that share this base, if any (e.g. "Light Belt" → "Arctic Binding", "Bane's Authority").
/// Empty - never null - when this base has no Set version.
/// </param>
/// <param name="UniqueVariants">
/// Names of Unique item that share this base, if any (e.g. "Monarch" → "Stormshield").
/// Empty - never null - when this base has no Unique version.
/// </param>
public sealed record ItemBase(
  string Name,
  string DisplayGroup,
  ItemCategory Category,
  string? Tier,
  RarityFlags ApplicableRarities,
  int? MaxSockets,
  IReadOnlyList<string> SetVariants,
  IReadOnlyList<string> UniqueVariants
);
