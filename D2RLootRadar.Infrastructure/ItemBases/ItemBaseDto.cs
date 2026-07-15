namespace D2RLootRadar.Infrastructure.ItemBases;

/// <summary>
/// Mirrors the item-bases.json schema exactly.
/// </summary>
/// <param name="Tier">
/// The item's power tier within its Type (e.g. "Normal"/"Exceptional"/"Elite" for weapons and armor,
/// "Low"/"Mid"/"High" for runes, "Chipped"/"Flawed"/... for gems).
/// Nullable - Ring, Amulet, Charm, Jewel omit this field.
/// </param>
/// <param name="Qualities">
/// Which <see cref="Domain.Loot.RarityFlags"/> this base can actually appear as -
/// member names match exactly (e.g. "EtherealSocketed", "Superior").
/// Omitted from the JSON - ans therefore null here - for bases with no rarity system at all (Rune, Gem, Material).
/// </param>
/// <param name="MaxSockets">
/// Maximum sockets this base can roll, shown in the watch-list UI's info tooltip.
/// Null when the base can't be socketed at all.
/// </param>
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
  string? Tier,
  string Base,
  string[]? Qualities,
  int? MaxSockets,
  string[]? Sets,
  string[]? Uniques
);
