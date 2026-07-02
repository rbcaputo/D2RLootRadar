using D2RLootRadar.Domain.Loot;

namespace D2RLootRadar.Application.Contracts;

/// <summary>
/// Provides access to the complete item base catalog.
/// </summary>
public interface IItemBaseCatalog
{
  /// <summary>
  /// Returns every item base known to the catalog, loaded once at startup from
  /// <c>item-bases.json</c> and cached for the lifetime of the application.
  /// </summary>
  IReadOnlyCollection<ItemBase> GetAll();
}
