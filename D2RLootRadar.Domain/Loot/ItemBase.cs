namespace D2RLootRadar.Domain.Loot;

/// <summary>
/// Represents a D2R item base as it appears on a floor label.
/// </summary>
/// <param name="Name">
/// The exact text that appears on the floor label - the value the OCR pipeline matches against.
/// </param>
/// <param name="Category">
/// Broad domain used for ordering in the UI and any future category-scoped logic.
/// </param>
/// <param name="DisplayGroup">
/// Sub-classification derived from the item database taxonomy (Subtype ?? Type).
/// Drives the grouping in the config UI - e.g. "Axe", "Sword", "Circlet", "Rune", "Key".
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
  ItemCategory Category,
  string DisplayGroup,
  IReadOnlyList<string> SetVariants,
  IReadOnlyList<string> UniqueVariants
);
