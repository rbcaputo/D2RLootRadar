using D2RLootRadar.Application.Catalog;
using D2RLootRadar.Domain.Loot;

namespace D2RLootRadar.Tests.Catalog;

public class CatalogFilterMatcherTests
{
  private static ItemBase Item(
    string name = "Shako",
    string? tier = null,
    IReadOnlyList<string>? setVariants = null,
    IReadOnlyList<string>? uniqueVariants = null
  ) => new(
    name,
    DisplayGroup: "Circlet",
    Category: ItemCategory.Helmet,
    tier,
    ApplicableRarities: RarityFlags.Normal | RarityFlags.Magic | RarityFlags.Rare | RarityFlags.Unique,
    MaxSockets: 2,
    setVariants ?? [],
    uniqueVariants ?? []
  );

  private static CatalogFilter Filter(
    string searchText = "",
    IReadOnlySet<string>? tiers = null,
    IReadOnlySet<string>? categories = null,
    bool requireUniqueVariant = false,
    bool requireSetVariant = false
  ) => new(
    searchText,
    tiers ?? new HashSet<string>(),
    categories ?? new HashSet<string>(),
    requireUniqueVariant,
    requireSetVariant
  );

  // --- MatchesSearch -----

  [Fact]
  public void MatchesSearch_BlankSearchText_AlwaysMatches()
  {
    ItemBase item = Item(name: "Shako");

    Assert.True(CatalogFilterMatcher.MatchesSearch(item.Name, item.SetVariants, item.UniqueVariants, ""));
    Assert.True(CatalogFilterMatcher.MatchesSearch(item.Name, item.SetVariants, item.UniqueVariants, " "));
  }

  [Fact]
  public void MatchesSearch_SubstringOfName_Matches()
  {
    ItemBase item = Item(name: "Shako");

    Assert.True(CatalogFilterMatcher.MatchesSearch(item.Name, item.SetVariants, item.UniqueVariants, "sha"));
  }

  [Fact]
  public void MatchesSearch_IsCaseInsensitive()
  {
    ItemBase item = Item(name: "Shako");

    Assert.True(CatalogFilterMatcher.MatchesSearch(item.Name, item.SetVariants, item.UniqueVariants, "SHAKO"));
  }

  [Fact]
  public void MatchesSearch_MatchesUniqueVariantName_NotJustBaseName()
  {
    // Users search for the famous Unique name ("Harlequin Crest"), not the undelying base ("Shako") -
    // this is the whole reason the seartch checks against variant names at all.
    ItemBase item = Item(name: "Shakoo", uniqueVariants: ["Harlequin Crest"]);

    Assert.True(CatalogFilterMatcher.MatchesSearch(item.Name, item.SetVariants, item.UniqueVariants, "Harlequin"));
  }

  [Fact]
  public void MatchesSearch_MatchesSetVariantName_NotJustBaseName()
  {
    ItemBase item = Item(name: "Light Belt", setVariants: ["Arctic Binding", "Bane's Authority"]);

    Assert.True(CatalogFilterMatcher.MatchesSearch(item.Name, item.SetVariants, item.UniqueVariants, "bane"));
  }

  [Fact]
  public void MatchesSearch_NoMatchAnywhere_DoesNotMatch()
  {
    ItemBase item = Item(name: "Shako", uniqueVariants: ["Harlequin Crest"]);

    Assert.False(CatalogFilterMatcher.MatchesSearch(item.Name, item.SetVariants, item.UniqueVariants, "monarch"));
  }

  // --- MatchesTier -----

  [Fact]
  public void MatchesTier_NoTiersSelected_AlwaysMatch()
  {
    // Including a base with no tier at all (Ring, Amulet, Charm, Jewel) -
    // "no filter active" still means "everything passes", even for items outside the Tier vocabulary entirely.
    Assert.True(CatalogFilterMatcher.MatchesTier(null, new HashSet<string>()));
    Assert.True(CatalogFilterMatcher.MatchesTier("Normal", new HashSet<string>()));
  }

  [Fact]
  public void MatchesTier_NullTierWithActiveFilter_DoesNotMatch()
    // A Ring/Amulet/Charm/Jewel has no Tier at all - once any Tier filter is active it's correctly excluded,
    // not a bug to special-case around.
    => Assert.False(CatalogFilterMatcher.MatchesTier(null, new HashSet<string>() { "Normal" }));

  [Fact]
  public void MatchesTier_TierInSelectedSet_Matches()
    => Assert.True(CatalogFilterMatcher.MatchesTier("Elite", new HashSet<string>() { "Noirmal", "Elite" }));

  [Fact]
  public void MatchesTier_TierNotInSelectedSet_DoesNotMatch()
    => Assert.False(CatalogFilterMatcher.MatchesTier("Exceptional", new HashSet<string>() { "Normal", "Elite" }));

  [Fact]
  public void MatchesTier_RuneSpecificVocabulary_NeverMatchesTheThreeFilterOptions()
    // Runes use "Low"/"Mid"/"High", an entriely different vocabulary from the filter's
    // Normal/Exceptional/Elite options - this is intentional esclusion, not a gap.
    => Assert.False(CatalogFilterMatcher.MatchesTier("Low", new HashSet<string>() { "Normal", "Exceptional", "Elite" }));

  // --- MatchesVariants -----

  [Fact]
  public void MatchesVariants_NeitherToggleActive_AlwaysMatches()
    => Assert.True(CatalogFilterMatcher.MatchesVariants(
      hasUniqueVariants: false,
      hasSetVariants: false,
      requireUniqueVariant: false,
      requireSetVariant: false
    ));

  [Fact]
  public void MatchesVariants_HasUniqueRequired_ItemHasUnique_Matches()
    => Assert.True(CatalogFilterMatcher.MatchesVariants(
      hasUniqueVariants: true,
      hasSetVariants: false,
      requireUniqueVariant: true,
      requireSetVariant: false
    ));

  [Fact]
  public void MatchesVariants_HasUniqueRequired_ItemOnlyHasSet_DoesNotMatch()
    // Having a Set variant doesn't satisfy "Has Unique" - the two toggles are independent constraints,
    // not one generic "has any variant" switch.
    => Assert.False(CatalogFilterMatcher.MatchesVariants(
      hasUniqueVariants: false,
      hasSetVariants: true,
      requireUniqueVariant: true,
      requireSetVariant: false
    ));

  [Fact]
  public void MatchesVariants_BothToggledOn_ItemHasOnlyOne_StillMatches()
    // OR within the group: turning both toggles on widens the result set (anything with either kind of variant),
    // it doesn't narrow it to items with both at once.
    => Assert.True(CatalogFilterMatcher.MatchesVariants(
      hasUniqueVariants: true,
      hasSetVariants: false,
      requireUniqueVariant: true,
      requireSetVariant: true
    ));

  [Fact]
  public void MatchesVariants_BothToggledOn_ItemHasNeither_DoesNotMatch()
    => Assert.False(CatalogFilterMatcher.MatchesVariants(
      hasUniqueVariants: false,
      hasSetVariants: false,
      requireUniqueVariant: true,
      requireSetVariant: true
    ));

  // --- Matches (full composition) -----

  [Fact]
  public void Matches_EmptyFilter_AlwaysMatches()
  {
    ItemBase item = Item(name: "Shako", tier: null);

    Assert.True(CatalogFilterMatcher.Matches(item, CatalogFilter.Empty));
  }

  [Fact]
  public void Matches_SearchMatchesButTierExcludes_OverallDoesNotMatch()
  {
    // Every dimension is AND'd together - a hit on search alone isn't enough if Tier says no.
    ItemBase item = Item(name: "Phase Blade", tier: "Elite");
    CatalogFilter filter = Filter(searchText: "phase", tiers: new HashSet<string>() { "Normal" });

    Assert.False(CatalogFilterMatcher.Matches(item, filter));
  }

  [Fact]
  public void Matches_AllDimensionsSatisfied_Matches()
  {
    ItemBase item = Item(name: "Monarch", tier: null, uniqueVariants: ["Stormshield"]);
    CatalogFilter filter = Filter(searchText: "strom", requireUniqueVariant: true);

    Assert.True(CatalogFilterMatcher.Matches(item, filter));
  }

  [Fact]
  public void Matches_SearchDoesNotMatch_OverallDoesNotMatch()
  {
    ItemBase item = Item(name: "Monarch");
    CatalogFilter filter = Filter(searchText: "Stormshield's exact base name is Monarch but this text is not");

    Assert.False(CatalogFilterMatcher.Matches(item, filter));
  }
}
