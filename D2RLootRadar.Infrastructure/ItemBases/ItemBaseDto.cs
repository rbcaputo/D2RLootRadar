namespace D2RLootRadar.Infrastructure.ItemBases;

/// <summary>
/// Mirrors the item-bases.json schema exactly.
/// </summary>
/// <param name="Sets">
/// Names of Set items that share this base, if any (e.g. "Light Belt" → ["Arctic Binding", "Bane's Authority"]).
/// Omitted from the JSON - and therefore null here - for bases with no Set version.
/// A base can have more than one, since several Sets can reuse the same base.
/// </param>
/// <param name="Uniques">
/// Names of Unique items that share this base, if any (e.g. "Monarch" → ["Stormshield"]).
/// Ommitted from the JSON - and therefore null here - for bases with no Unique version.
/// A base can have more than one (e.g. "Ring" lists all Unique rings).
/// </param>
public sealed record ItemBaseDto(
  string Supertype,
  string Type,
  string? Subtype,
  string? Quality, // nullable - Ring, Amulet, Charm, Jewel omit this field
  string Base,
  string[]? Sets,
  string[]? Uniques
);
