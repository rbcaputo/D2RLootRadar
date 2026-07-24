using D2RLootRadar.Domain.Loot;

namespace D2RLootRadar.Application.Catalog;

/// <summary>
/// Pure predicate logic behind the main window's catalog search and filters.
/// 
/// <para>
/// Lives here rather than on the item view model so it can be unit tested directly against <see cref="ItemBase"/> -
/// the same data the catalog itself is built from - without needing a reference to the Desktop project's
/// WPF/ObservableObject machinery.
/// The view model layer (<c>ItemBaseViewModel</c>, <c>CategoryViewModel</c>) calls into this rather than
/// reimplementing it.
/// </para>
/// </summary>
public static class CatalogFilterMatcher
{
  /// <summary>
  /// Whether <paramref name="item"/> matches every constraint in <paramref name="filter"/> at once -
  /// search AND Tier AND variants.
  /// Each individual check already treats "no active constraint" as "everything passes"
  /// (blank search text, empty Tier set, both variant toggles off), so that composition falls out for free;
  /// no special-casing is needed here for "only one constraint is actually active".
  /// 
  /// <para>
  /// Deliberately does not evaluate <see cref="CatalogFilter.Categories"/> -
  /// unlike the other three, that constraint isn't meaningful per item.
  /// Every item sharing one category-group view model has the exact same <see cref="ItemBase.DisplayGroup"/> by
  /// construction, so "does this item's category match the filter" is always the same answer for every item in
  /// that group - the caller checks it once per group instead of once per item here.
  /// </para>
  /// </summary>
  public static bool Matches(ItemBase item, CatalogFilter filter)
    => MatchesSearch(item.Name, item.SetVariants, item.UniqueVariants, filter.SearchText) &&
       MatchesTier(item.Tier, filter.Tiers) &&
       MatchesVariants(
         item.UniqueVariants.Count > 0,
         item.SetVariants.Count > 0,
         filter.RequireUniqueVariant,
         filter.RequireSetVariant
       );

  /// <summary>
  /// Whether an item matches the main window's catalog seach box.
  /// A blank/whitespace-only <paramref name="searchText"/> means "no search active" - always matches.
  /// 
  /// <para>
  /// Matches against <paramref name="name"/> first, then <paramref name="setVariants"/> and
  /// <paramref name="uniqueVariants"/> - users usually search for the famous Set/Unique name ("Harlequin Crest")
  /// rather than the underlying base ("Shako"), so limiting the search to the base name alone would make the
  /// one case users actually search for the hardest to find.
  /// </para>
  /// </summary>
  public static bool MatchesSearch(
    string name,
    IReadOnlyList<string> setVariants,
    IReadOnlyList<string> uniqueVariants,
    string searchText
  )
  {
    if (string.IsNullOrWhiteSpace(searchText))
      return true;

    if (name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
      return true;

    foreach (string variant in setVariants)
      if (variant.Contains(searchText, StringComparison.OrdinalIgnoreCase))
        return true;

    foreach (string variant in uniqueVariants)
      if (variant.Contains(searchText, StringComparison.OrdinalIgnoreCase))
        return true;

    return false;
  }

  /// <summary>
  /// Whether an item matches the main window's Tier filter.
  /// 
  /// <para>
  /// An empty <paramref name="selectedTiers"/> means "no filter active" -
  /// always matches, same no-filter-means-everything-passes convention as <see cref="MatchesSearch"/>.
  /// Otherwise, matches if <paramref name="tier"/> is any of the selected values (multi-select is OR within the group -
  /// see <see cref="CatalogFilter"/>'s remarks for why that's the only sensible reading for a field where an
  /// item can only ever hold one value).
  /// </para>
  /// 
  /// <para>
  /// Deliberately an exact match, not a substring/fuzzy one - Tier is a closed,
  /// known vocabulary per category, not free text a user is typing.
  /// An item whose own <paramref name="tier"/> is null (Ring, Amulet, Charm, Jewel) or uses the Rune/Gem-specific
  /// vocabulary ("Low", "Chipped", etc.) will never equal any of the filter's three options
  /// (Normal/Exceptional/Elite) - that's intentional, not a gap: those bases genuinely don't have a 
  /// Normal/Exceptional/Elite tier to filter by, so they're correctly excluded whenever any tier is selected.
  /// </para>
  /// </summary>
  public static bool MatchesTier(string? tier, IReadOnlySet<string> selectedTiers)
    => selectedTiers.Count == 0 ||
       (tier is not null && selectedTiers.Contains(tier));

  /// <summary>
  /// Whether an item matches the main window's "Has Unique"/"Has Set" variant filters.
  /// 
  /// <para>
  /// Both false means "no filter active" - always matches.
  /// Otherwise, OR within the group like <see cref="MatchesTier"/> - unlike Tier though,
  /// an item genuinely can satisfy both at once (have both a Unique and a Set variant),
  /// so turning on both options widens the result set (anything with either kind of variant)
  /// rather than narrowing it to only items with both.
  /// Thet's the same OR-within-group rule as every other filter group;
  /// it just reads more intuitively here since the "OR, not AND" choice has a real behavioral
  /// consequence to notice, where for Tier it's the only option that could ever match anything at all.
  /// </para>
  /// </summary>
  public static bool MatchesVariants(
    bool hasUniqueVariants,
    bool hasSetVariants,
    bool requireUniqueVariant,
    bool requireSetVariant
  )
  {
    if (!requireUniqueVariant && !requireSetVariant)
      return true;

    return (requireUniqueVariant && hasUniqueVariants) ||
           (requireSetVariant && hasSetVariants);
  }
}
