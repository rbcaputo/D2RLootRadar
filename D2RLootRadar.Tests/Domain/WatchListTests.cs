using D2RLootRadar.Domain.Loot;

namespace D2RLootRadar.Tests.Domain;

public class WatchListTests
{
  private static WatchedItem Watched(
    string name,
    string displayGroup,
    ItemCategory category,
    string? tier = "Elite",
    RarityFlags selected = RarityFlags.Unique
  ) => new(new(name, displayGroup, category, tier, RarityFlags.None, 0, [], []), selected);

  [Fact]
  public void Constructor_NullItems_Throws()
    => Assert.Throws<ArgumentNullException>(() => new WatchList(null!));

  [Fact]
  public void Items_ExactDuplicateItemBase_IsDeduplicated()
  {
    // Same Name and SelectedRarities → record equality treats these as one entry.
    WatchedItem a = Watched("Monarch", "Shield", ItemCategory.Shield);
    WatchedItem b = Watched("Monarch", "Shield", ItemCategory.Shield);

    WatchList watchList = new([a, b]);

    Assert.Single(watchList.Items);
  }

  [Fact]
  public void Items_SameNameDifferentCategory_IsNotDeduplicated()
  {
    // Pins the behavior documented on WatchList:
    // deduplication uses full record equality, not just Name,
    // so two entries that only share a Name are kept distinct.
    WatchedItem a = Watched("Monarch", "Shield", ItemCategory.Shield);
    WatchedItem b = Watched("Monarch", "Mace", ItemCategory.Weapon, "Elite");

    WatchList watchList = new([a, b]);

    Assert.Equal(2, watchList.Items.Count);
  }

  [Fact]
  public void Items_SameItemDifferentSelectedRarities_IsNotDeduplicated()
  {
    // Same catalog Base but a different rarity selection is a genuinely different entry.
    WatchedItem a = Watched("Monarch", "Shield", ItemCategory.Shield, selected: RarityFlags.Magic);
    WatchedItem b = Watched("Monarch", "Shield", ItemCategory.Shield, selected: RarityFlags.Unique);

    WatchList watchList = new([a, b]);

    Assert.Equal(2, watchList.Items.Count);
  }

  [Fact]
  public void Items_DistinctItems_AreAllPreserved()
  {
    WatchedItem[] items = [
      Watched("Monarch", "Shield", ItemCategory.Shield),
      Watched("Phase Blade", "Sword", ItemCategory.Weapon),
      Watched("Ber Rune", "Rune", ItemCategory.Rune, tier: null, RarityFlags.RuneMaterial)
    ];

    WatchList watchList = new(items);

    Assert.Equal(3, watchList.Items.Count);
  }
}
