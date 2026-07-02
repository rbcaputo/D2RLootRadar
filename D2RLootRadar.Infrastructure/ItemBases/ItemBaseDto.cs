namespace D2RLootRadar.Infrastructure.ItemBases;

/// <summary>
/// Mirrors the item-bases.json schema exactly.
/// </summary>
public sealed record ItemBaseDto(
  string Supertype,
  string Type,
  string? Subtype,
  string? Quality, // nullable - Ring, Amulet, Charm, Jewel omit this field
  string Base
);
