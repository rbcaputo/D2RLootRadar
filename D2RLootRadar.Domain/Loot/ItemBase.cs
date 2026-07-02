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
public sealed record ItemBase(string Name, ItemCategory Category, string DisplayGroup);
