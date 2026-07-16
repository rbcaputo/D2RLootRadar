namespace D2RLootRadar.Domain.Loot;

/// <summary>
/// Represents the user's selected item bases - and, per item, which rarities to alert for - a single detection pass.
/// 
/// <para>
/// Deduplicates by <see cref="WatchedItem"/>'s default (record-generated) equality.
/// This class does not itself perform case-insensitive comparison on item names;
/// the case-insensitive matching that OCR requires happens upstream,
/// where the caller resolves the user's saved selection against the catalog using
/// <see cref="StringComparer.OrdinalIgnoreCase"/> before constructing this list, and downstream,
/// where <c>IFuzzyMatcher</c> compares OCR text against these names case-insensitively.
/// </para>
/// </summary>
public sealed class WatchList
{
  private readonly HashSet<WatchedItem> _items;

  /// <summary>
  /// Builds an immutable snapshot of the given items, deduplicating exact results.
  /// </summary>
  public WatchList(IEnumerable<WatchedItem> items)
  {
    ArgumentNullException.ThrowIfNull(items);

    _items = [.. items];
  }

  /// <summary>
  /// Returns all selected item bases.
  /// </summary>
  public IReadOnlyCollection<WatchedItem> Items
  => _items;
}
