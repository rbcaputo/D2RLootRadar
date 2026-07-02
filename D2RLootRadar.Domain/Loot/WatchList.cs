namespace D2RLootRadar.Domain.Loot;

/// <summary>
/// Represents the user's selected item bases for a single detection pass.
/// 
/// <para>
/// Deduplicates by <see cref="ItemBase"/>'s default (case-insensitive, record-generated) equality.
/// This class does not itself perform case-insensitive comparison;
/// the case-insensitive matching that OCR requires happens upstream,
/// where the caller resolves the user's saved selection against the gatalog using
/// <see cref="StringComparer.OrdinalIgnoreCase"/> before constructing this list, and downstream,
/// where <c>IFuzzyMatcher</c> compares OCR text against these names case-insensitively.
/// </para>
/// </summary>
public sealed class WatchList
{
  private readonly HashSet<ItemBase> _items;

  /// <summary>
  /// Builds an immutable snapshot of the given items, deduplicating exact results.
  /// </summary>
  public WatchList(IEnumerable<ItemBase> items)
  {
    ArgumentNullException.ThrowIfNull(items);

    _items = [.. items];
  }

  /// <summary>
  /// Returns all selected item bases.
  /// </summary>
  public IReadOnlyCollection<ItemBase> Items
  => _items;
}
