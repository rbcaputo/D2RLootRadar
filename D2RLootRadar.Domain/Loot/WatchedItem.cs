namespace D2RLootRadar.Domain.Loot;

/// <summary>
/// A single entry in the user's watch list:
/// a catalog item paired with which <see cref="RarityFlags"/> the user wants to be alerted for,
/// specifically for this item.
/// 
/// <see cref="SelectedRarities"/> is never <see cref="RarityFlags.None"/> here -
/// an item with no rarities selected isn't watched at all, so it never becomes a
/// <see cref="WatchedItem"/> in the first place (see <c>LootMonitoringService.BuildWatchList</c>).
/// </summary>
public sealed record WatchedItem(ItemBase Base, RarityFlags SelectedRarities);
