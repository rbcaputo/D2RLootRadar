using CommunityToolkit.Mvvm.ComponentModel;
using D2RLootRadar.Domain.Loot;

namespace D2RLootRadar.Desktop.ViewModels;

/// <summary>
/// Represents a single item base in the watch list UI:
/// a name plus up to seven independently-toggleable rarities
/// (only the ones <see cref="ApplicableRarities"/> actually permit for this base are shown at all).
/// 
/// <para>
/// There is no separate "is this item selected" flag -
/// having at least one rarity active *is* what makes the item watched.
/// An item with <see cref="SelectedRarities"/> equal to <see cref="RarityFlags.None"/> is
/// simply not part of the watch list.
/// </para>
/// </summary>
public partial class ItemBaseViewModel(
  string name,
  RarityFlags applicableRarities,
  RarityFlags selectedRarities,
  int? maxSockets,
  IReadOnlyList<string> setVariants,
  IReadOnlyList<string> uniqueVariants
) : ObservableObject
{
  /// <summary>
  /// Defensive against a stale bit that no longer applies -
  /// e.g. settings.json saved before a catalog change (this session's Rune/Material Normal → RuneMaterial/Shard
  /// reclassification being the exact case that motivated this), or simply hand-edited settings.json.
  /// Without this, a leftover bit outside ApplicableRarities would never surface in the UI
  /// (no checkbox exists for it) but would still silently count toward SelectedRarities.
  /// </summary>
  private RarityFlags _selectedRarities = selectedRarities & applicableRarities;

  /// <summary>
  /// Whether the rarity picker popup is currently open for this row.
  /// Pure UI state - never persisted, and resent to closed every time the app starts.
  /// </summary>
  [ObservableProperty]
  private bool _isPopupOpen;

  /// <summary>
  /// Whether this row should render under the current catalog search filter.
  /// Always true when no search is active - see <see cref="MatchesSearch"/>.
  /// </summary>
  [ObservableProperty]
  private bool _isVisible = true;

  /// <summary>
  /// The item base name, matching the catalog and used as the OCR match target.
  /// </summary>
  public string Name { get; } = name;

  /// <summary>
  /// Which rarities this base can actually appear as -  drives which of the seven rarities render at all.
  /// Sourceds from the catalog's <c>Qualities</c> data, not derived here.
  /// </summary>
  public RarityFlags ApplicableRarities { get; } = applicableRarities;

  /// <summary>
  /// Maximum sockets this base can roll, shown in the info icon's tooltip.
  /// Null when the base can't be socketed at all (e.g. Charms, most jewelry, etc.).
  /// </summary>
  public int? MaxSockets { get; } = maxSockets;

  /// <summary>
  /// Names of Set items that shate this base, shown in the name's hover tooltip.
  /// </summary>
  public IReadOnlyList<string> SetVariants { get; } = setVariants;

  /// <summary>
  /// Names of Unique items that share this base, shown in the name's hover tooltip.
  /// </summary>
  public IReadOnlyList<string> UniqueVariants { get; } = uniqueVariants;

  /// <summary>
  /// The user's current rarity selection for this item,
  /// read by <c>MainViewModel</c> when persisting settings.
  /// </summary>
  public RarityFlags SelectedRarities
    => _selectedRarities;

  /// <summary>
  /// Whether this base can roll sockets at all - false items don't get a sockets line in the info tooltip.
  /// </summary>
  public bool HasMaxSockets
    => MaxSockets > 0;

  /// <summary>
  /// Whether hovering the name should show the variant-name tooltip at all -
  /// false for the common case of a base with neither a Set nor a Unique version,
  /// so those rows never pop an empty tooltip.
  /// </summary>
  public bool HasVariants
    => SetVariants.Count > 0 || UniqueVariants.Count > 0;

  /// <summary>
  /// Whether the info icon has anything at all to show -
  /// false for the common case of a plain, unsocketable base with no Set or Unique version,
  /// so those rows never render an icon that just opens an empty tooltip.
  /// </summary>
  public bool HasInfo
    => HasMaxSockets || HasVariants;

  /// <summary>
  /// Whether this base has more than one applicable rarity to choose between.
  /// False for Rune/Gem/Material (always just <see cref="RarityFlags.Normal"/>) -
  /// those rows get a plain wacthed/not-watched checkbox instead of the rarity popup,
  /// since there's nothing to actually choose there.
  /// </summary>
  public bool IsSingleRarity
    => (int)ApplicableRarities == 0 || (ApplicableRarities & (ApplicableRarities - 1)) == 0;

  /// <summary>
  /// Whether at least one rarity is currently selected for this item -
  /// drives the "None" placeholder in the popup toggle's summary when false.
  /// </summary>
  public bool HasAnySelection
    => _selectedRarities != RarityFlags.None;

  // --- Selection visibility -----
  // Whether this base can appear as each rarity at all.

  public bool ShowNormal
    => ApplicableRarities.HasFlag(RarityFlags.Normal);
  public bool ShowEtheralSocketed
    => ApplicableRarities.HasFlag(RarityFlags.EtherealSocketed);
  public bool ShowMagic
    => ApplicableRarities.HasFlag(RarityFlags.Magic);
  public bool ShowRare
    => ApplicableRarities.HasFlag(RarityFlags.Rare);
  public bool ShowSet
    => ApplicableRarities.HasFlag(RarityFlags.Set);
  public bool ShowUnique
    => ApplicableRarities.HasFlag(RarityFlags.Unique);
  public bool ShowSuperior
    => ApplicableRarities.HasFlag(RarityFlags.Superior);

  // --- Selection state -----
  // Whether the user has each one selected.
  //
  // Each setter toggles just its own bit, so the popup's checkboxes can be found directly and
  // freely combined (e.g. EtherealSocketed + Set + Superior all on at once).

  public bool IsNormalSelected
  {
    get => _selectedRarities.HasFlag(RarityFlags.Normal);
    set => SetRarity(RarityFlags.Normal, value);
  }

  public bool IsEtherealSocketedSelected
  {
    get => _selectedRarities.HasFlag(RarityFlags.EtherealSocketed);
    set => SetRarity(RarityFlags.EtherealSocketed, value);
  }

  public bool IsMagicSelected
  {
    get => _selectedRarities.HasFlag(RarityFlags.Magic);
    set => SetRarity(RarityFlags.Magic, value);
  }

  public bool IsRareSelected
  {
    get => _selectedRarities.HasFlag(RarityFlags.Rare);
    set => SetRarity(RarityFlags.Rare, value);
  }

  public bool IsSetSelected
  {
    get => _selectedRarities.HasFlag(RarityFlags.Set);
    set => SetRarity(RarityFlags.Set, value);
  }

  public bool IsUniqueSelected
  {
    get => _selectedRarities.HasFlag(RarityFlags.Unique);
    set => SetRarity(RarityFlags.Unique, value);
  }

  public bool IsSuperiorSelected
  {
    get => _selectedRarities.HasFlag(RarityFlags.Superior);
    set => SetRarity(RarityFlags.Superior, value);
  }

  /// <summary>
  /// Whether this item matches a catalog search term.
  /// 
  /// <para>
  /// Plain case-insensitive substring containment, not the app's OCR <c>IFuzzyMatcher</c> -
  /// that matcher is tuned for scoring noisy, already-complete OCR tokens against catalog names,
  /// not for incremental "type-to-find" filtering where the user's input is deliberately partial
  /// (e.g. "sho" while typing "Shako") and every extra keystroke should only narrow the result set,
  /// never re-score it.
  /// Substring matching is also directly predictable to the person typing, which an edit-distance score isn't.
  /// </para>
  /// 
  /// <para>
  /// Matches against <see cref="Name"/> first, then <see cref="SetVariants"/> and <see cref="UniqueVariants"/> -
  /// users usually search for the famous Set/Unique name ("Harlequin Crest) rather than the
  /// underlying base ("Shako"), so limiting the search to <see cref="Name"/> alone would make the
  /// one case users actually search for the hardest to find.
  /// </para>
  /// </summary>
  public bool MatchesSearch(string searchText)
  {
    if (string.IsNullOrWhiteSpace(searchText))
      return true;

    if (Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
      return true;

    foreach (string variant in SetVariants)
      if (variant.Contains(searchText, StringComparison.OrdinalIgnoreCase))
        return true;

    foreach (string variant in UniqueVariants)
      if (variant.Contains(searchText, StringComparison.OrdinalIgnoreCase))
        return true;

    return false;
  }

  /// <summary>
  /// Tri-state selection for this item's checkbox, mirroring the category header's tri-state pattern at the item level:
  /// false = nothing selected, true = every applicable rarity selected, null = some but not all.
  /// Setting it to a non-null value delegates to <see cref="SetAllRarities"/>;
  /// setting it via a click on an indeterminate checkbox (value arrives as true from WPF) selects all.
  /// </summary>
  public bool? AllRaritiesSelected
  {
    get
    {
      if (_selectedRarities == RarityFlags.None)
        return false;
      if (_selectedRarities == ApplicableRarities)
        return true;

      return null;
    }
    set => SetAllRarities(value ?? true);
  }

  /// <summary>
  /// Turns a single rarity bit on or off, leaving every other currently-selected bit untouched -
  /// this is what lets rarities stack (e.g. Set + Unique both on for the same item).
  /// </summary>
  private void SetRarity(RarityFlags flag, bool selected)
  {
    _selectedRarities = selected
      ? _selectedRarities | flag
      : _selectedRarities & ~flag;

    RaiseRaritiesChanged();
  }

  /// <summary>
  /// Bulk-sets this item's selection:
  /// every rarity this base can actually appear as
  /// ("select all" - only applicable rarities are turned on, not literally all seven), or none at all.
  /// Backs the category header's tri-state checkbox.
  /// </summary>
  public void SetAllRarities(bool watched)
  {
    _selectedRarities = watched
      ? ApplicableRarities
      : RarityFlags.None;

    RaiseRaritiesChanged();
  }

  private void RaiseRaritiesChanged()
  {
    OnPropertyChanged(nameof(AllRaritiesSelected));
    OnPropertyChanged(nameof(SelectedRarities));
    OnPropertyChanged(nameof(HasAnySelection));
    OnPropertyChanged(nameof(IsNormalSelected));
    OnPropertyChanged(nameof(IsEtherealSocketedSelected));
    OnPropertyChanged(nameof(IsMagicSelected));
    OnPropertyChanged(nameof(IsRareSelected));
    OnPropertyChanged(nameof(IsSetSelected));
    OnPropertyChanged(nameof(IsUniqueSelected));
    OnPropertyChanged(nameof(IsSuperiorSelected));
  }
}
