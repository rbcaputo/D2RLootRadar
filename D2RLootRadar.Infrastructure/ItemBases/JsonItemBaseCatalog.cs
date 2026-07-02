using D2RLootRadar.Application.Contracts;
using D2RLootRadar.Domain.Loot;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace D2RLootRadar.Infrastructure.ItemBases;

/// <summary>
/// Loads the item base catalog from <c>Data/item-bases.json</c> and maps each entry to
/// a domain <see cref="ItemBase"/>.
/// 
/// <para>JSON schema:</para>
/// <code>
/// {
///   "Supertype": "Weapon" | "Armor" | "Misc",
///   "Type": "One-Handed" | "Two-Handed" | "Belt" | "Torso" | ... | "Rune" | "Gem" | ...,
///   "Subtype": "Axe" | "Sword" | "Circlet" | "Pelt" | "Key" | ... (optional),
///   "Base": "Phase Blade" ← the floor-label text matched by OCR
/// }
/// </code>
/// 
/// <para>
/// <strong>DisplayGroup</strong> is derived as <c>Subtype ?? Type</c>,
/// giving each entry sub-classification (e.g. "Axe", "Circlet", "Rune", "Key") that the
/// UI uses for grouping instead of the coarser <see cref="ItemCategory"/>.
/// </para>
/// </summary>
public sealed class JsonItemBaseCatalog : IItemBaseCatalog
{
  private readonly IReadOnlyCollection<ItemBase> _items;
  private readonly ILogger<JsonItemBaseCatalog> _logger;

  private static readonly string FilePath =
    Path.Combine(AppContext.BaseDirectory, "Data", "item-bases.json");
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    WriteIndented = true
  };

  /// <summary>
  /// Eagerly loads and maps the entire catalog from disk.
  /// Throws <see cref="FileNotFoundException"/> immediately if the data file is missing,
  /// since a missing catalog makes the app unusable -
  /// failing fast at startup beats a silent empty watch list discovered later.
  /// </summary>
  public JsonItemBaseCatalog(ILogger<JsonItemBaseCatalog> logger)
  {
    _items = Load();
    _logger = logger;
  }

  /// <inheritdoc />
  public IReadOnlyCollection<ItemBase> GetAll()
    => _items;

  /// <summary>
  /// Reads and deserializes <c>Data/item-bases.json</c>,
  /// mapping each DTO to a domain <see cref="ItemBase"/>.
  /// </summary>
  private List<ItemBase> Load()
  {
    if (!File.Exists(FilePath))
      throw new FileNotFoundException(
        $"Item base file not found: '{FilePath}'. " +
        $"Ensure the file is set to CopyToOutputDirectory in the project."
      );

    string json = File.ReadAllText(FilePath);
    List<ItemBaseDto>? dtos
      = JsonSerializer.Deserialize<List<ItemBaseDto>>(json, JsonOptions);

    return dtos?.Select(Map).ToList() ?? [];
  }

  /// <summary>
  /// Maps a single JSON DTO to its domain <see cref="ItemBase"/>,
  /// resolving category and display group.
  /// </summary>
  private ItemBase Map(ItemBaseDto dto)
  {
    ItemCategory category = ResolveCategory(dto);
    string displayGroup = dto.Subtype ?? dto.Type;

    return new(dto.Base, category, displayGroup);
  }

  /// <summary>
  /// Maps Supertype + Type to the domain <see cref="ItemCategory"/>.
  /// Extend this switch as new Type values are added to the item-base.json.
  /// </summary>
  private ItemCategory ResolveCategory(ItemBaseDto dto)
    => (dto.Supertype, dto.Type) switch
    {
      ("Weapon", _) => ItemCategory.Weapon,

      ("Armor", "Torso") => ItemCategory.Torso,
      ("Armor", "Helmet") => ItemCategory.Helmet,
      ("Armor", "Belt") => ItemCategory.Belt,
      ("Armor", "Boots") => ItemCategory.Boots,
      ("Armor", "Gloves") => ItemCategory.Gloves,
      ("Armor", "Shield") => ItemCategory.Shield,

      (_, "Rune") => ItemCategory.Rune,
      (_, "Ring") => ItemCategory.Ring,
      (_, "Amulet") => ItemCategory.Amulet,
      (_, "Charm") => ItemCategory.Charm,
      (_, "Jewel") => ItemCategory.Jewel,
      (_, "Gem") => ItemCategory.Gem,
      (_, "Material") => ItemCategory.Material,

      _ => Fallback(dto)
    };

  /// <summary>
  /// Called when <see cref="ResolveCategory"/> hits a Supertype/Type combination it doesn't recognize.
  /// Logs a warning (so the gap gets noticed) and defaults to <see cref="ItemCategory.Weapon"/>
  /// rather than throwing, so one unrecognized entry in item-bases.json can't take down the whole catalog load.
  /// </summary>
  private ItemCategory Fallback(ItemBaseDto dto)
  {
    _logger.LogWarning(
      "Unknown type mapping: Supertype='{Supertype}' Type='{Type}'. " +
      "Defaulting to Weapon. Update ResolveCategory if this is intentional.",
      dto.Supertype,
      dto.Type
    );

    return ItemCategory.Weapon;
  }
}
