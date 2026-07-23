namespace D2RLootRadar.Desktop.ViewModels;

/// <summary>
/// One consistent snapshot of every active catalog filter, passed down from <see cref="MainViewModel"/> to
/// each <see cref="CategoryViewModel"/>/<see cref="ItemBaseViewModel"/> so filter composition only
/// has to be reasoned about in one place instead of at every call site that touches more than
/// one of these fileds.
/// </summary>
public sealed record CatalogFilter(
  string SearchText,
  IReadOnlySet<string> Tiers,
  IReadOnlySet<string> Categories,
  bool RequireUniqueVariant,
  bool RequireSetVariant
)
{
  public static readonly CatalogFilter Empty = new(
    string.Empty,
    new HashSet<string>(),
    new HashSet<string>(),
    false,
    false
  );

  /// <summary>
  /// Whether any filter constraint is currently active at all -
  /// drives <see cref="CategoryViewModel"/>'s expand-before-filtering/restore-after-clearing behavior.
  /// </summary>
  public bool IsActive
    => !string.IsNullOrWhiteSpace(SearchText) ||
       Tiers.Count > 0 ||
       Categories.Count > 0 ||
       RequireUniqueVariant ||
       RequireSetVariant;
}
