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
///   "Tier": ["Normal" | "Exceptional" | "Elite" | "Low" | "Mid" | "High" | ... (optional),
///   "Base": "Phase Blade" ← the floor-label text matched by OCR,
///   "Qualities": ["Normal", "EtherealSocketed", "Magic", "Rare", ...]
///     (optional - which RarityFlags this base can appear as; omitted entirely for Rune/Gem/Material,
///      which have no quality variation - see DeaultRarities for how each of those resolves instead),
///   "Sets": ["Arctic Binding, ...] (optional - Set items sharing this base, if any),
///   "Uniques": ["Stormshield, ...] (optional - Unique items sharing this base, if any),
///   "MaxSockets": 6 (optional - shown in the watch-list UI's info tooltip)
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
  /// The <see cref="ItemBaseDto.Subtype"/> value that marks a Material as a Worldstone Shard rather than
  /// a Key/Part/Essence/Statue - the one Material group with its own label color.
  /// </summary>
  private const string ShardSubtype = "Shard";

  /// <summary>
  /// Eagerly loads and maps the entire catalog from disk.
  /// Throws <see cref="FileNotFoundException"/> immediately if the data file is missing,
  /// since a missing catalog makes the app unusable -
  /// failing fast at startup beats a silent empty watch list discovered later.
  /// </summary>
  public JsonItemBaseCatalog(ILogger<JsonItemBaseCatalog> logger)
  {
    _logger = logger;
    _items = Load();
  }

  /// <inheritdoc />
  public IReadOnlyCollection<ItemBase> GetAll()
    => _items;

  /// <summary>
  /// Reads and deserializes <c>Data/item-bases.json</c>, mapping each DTO to a domain <see cref="ItemBase"/>.
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
  /// Maps a single JSON DTO to its domain <see cref="ItemBase"/>, resolving category and display group.
  /// </summary>
  private ItemBase Map(ItemBaseDto dto)
  {
    ItemCategory category = ResolveCategory(dto);
    string displayGroup = dto.Subtype ?? dto.Type;
    RarityFlags applicableRarities = ParseQualities(dto, category);

    return new(
      dto.Base,
      displayGroup,
      category,
      dto.Tier,
      applicableRarities,
      dto.MaxSockets,
      dto.Sets ?? [],
      dto.Uniques ?? []
    );
  }

  /// <summary>
  /// Parses a DTO's <see cref="ItemBaseDto.Qualities"/> strings into a single <see cref="RarityFlags"/> value.
  /// Member names are matched exactly (case-sensitive) - the JSON is expected to spell them the same way the enum does.
  /// A null or absent array (Rune/Gem/Material) yields <see cref="RarityFlags.None"/>.
  /// An entry that doesn't match any <see cref="RarityFlags"/> member is logged and skipped,
  /// rather than failing the whole catalog load over one bad string.
  /// </summary>
  private RarityFlags ParseQualities(ItemBaseDto dto, ItemCategory category)
  {
    if (dto.Qualities is null)
      return DefaultRarities(dto, category);

    RarityFlags result = RarityFlags.None;

    foreach (string quality in dto.Qualities)
      if (Enum.TryParse(quality, ignoreCase: false, out RarityFlags flag))
        result |= flag;
      else
        _logger.LogWarning(
          "Unrecognized quality '{Quality}' for base '{Base}' - ignored. " +
          "Check spelling against the RarityFlags enum member names.",
          quality,
          dto.Base
        );

    return result;
  }

  /// <summary>
  /// The single fixed label colorfor a base with no quality variation at all -
  /// Gem is white (<see cref="RarityFlags.Normal"/>), Rune and every non-Shard Material are
  /// orange (<see cref="RarityFlags.RuneMaterial"/>), and Worldstone Shards are red (<see cref="RarityFlags.Shard"/>).
  /// Keeps these flowing through the same selection/watch-list/matching pipeline as every other base,
  /// instead of needing a separate "is this item wacthed at all" concept.
  /// </summary>
  private RarityFlags DefaultRarities(ItemBaseDto dto, ItemCategory category)
    => category switch
    {
      ItemCategory.Gem => RarityFlags.Normal,
      ItemCategory.Rune => RarityFlags.RuneMaterial,
      ItemCategory.Material when dto.Subtype == ShardSubtype => RarityFlags.Shard,
      ItemCategory.Material => RarityFlags.RuneMaterial,

      // Shouldn't happen -
      // every category without an explicit Qualities array is one of the above.
      // Defaulting to Normal (rather than None) keeps a mis-tagged entry selectable,
      // just wrongly colored, instead of silently unwatchable.
      _ => LogUnexpectedFlaglessCategory(dto, category)
    };

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

      _ => LogUnexpectedTypeSubtypeCombination(dto)
    };

  private RarityFlags LogUnexpectedFlaglessCategory(ItemBaseDto dto, ItemCategory category)
  {
    _logger.LogWarning(
      "Base '{Base}' in category '{Category}' has no Qualities array but isn't Gem/Rune/Material - " +
      "defaulting to Notmal. Update DefaultRarities is this category is meant to be flagless too.",
      dto.Base,
      category
    );

    return RarityFlags.Normal;
  }

  /// <summary>
  /// Called when <see cref="ResolveCategory"/> hits a Supertype/Type combination it doesn't recognize.
  /// Logs a warning (so the gap gets noticed) and defaults to <see cref="ItemCategory.Weapon"/>
  /// rather than throwing, so one unrecognized entry in item-bases.json can't take down the whole catalog load.
  /// </summary>
  private ItemCategory LogUnexpectedTypeSubtypeCombination(ItemBaseDto dto)
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
