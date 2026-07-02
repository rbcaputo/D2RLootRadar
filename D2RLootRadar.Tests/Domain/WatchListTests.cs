using D2RLootRadar.Domain.Loot;

namespace D2RLootRadar.Tests.Domain;

public class WatchListTests
{
  [Fact]
  public void Constructor_NullItems_Throws()
    => Assert.Throws<ArgumentNullException>(() => new WatchList(null!));

  [Fact]
  public void Items_ExactDuplicateItemBase_IsDeduplicated()
  {
    // Same Name, Category, and DisplayGroup → record equality treats these as one item.
    ItemBase a = new("Monarch", ItemCategory.Shield, "Shield");
    ItemBase b = new("Monarch", ItemCategory.Shield, "Shield");

    WatchList watchList = new([a, b]);

    Assert.Single(watchList.Items);
  }

  [Fact]
  public void Items_SameNameDifferentCategory_IsNotDeduplicated()
  {
    // Pins the behavior documented on WatchList: deduplication uses full record equality,
    // not just Name, so two entries that only share a Name are kept distinct.
    ItemBase a = new("Monarch", ItemCategory.Shield, "Shield");
    ItemBase b = new("Monarch", ItemCategory.Weapon, "Mace");

    WatchList watchList = new([a, b]);

    Assert.Equal(2, watchList.Items.Count);
  }

  [Fact]
  public void Items_DistinctItems_AreAllPreserved()
  {
    ItemBase[] items = [
      new("Monarch", ItemCategory.Shield, "Shield"),
      new("Phase Blade", ItemCategory.Weapon, "Sword"),
      new("Ber Rune", ItemCategory.Rune, "Rune")
    ];

    WatchList watchList = new(items);

    Assert.Equal(3, watchList.Items.Count);
  }
}
