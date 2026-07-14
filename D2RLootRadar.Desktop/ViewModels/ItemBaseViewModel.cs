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
  IReadOnlyList<string> setVariants,
  IReadOnlyList<string> uniqueVariants
) : ObservableObject
{
  private RarityFlags _selectedRarities = selectedRarities;

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
  /// Names of Set items that shate this base, shown in the name's hover tooltip.
  /// </summary>
  public IReadOnlyList<string> SetVariants { get; } = setVariants;

  /// <summary>
  /// Names of Unique items that share this base, shown in the name's hover tooltip.
  /// </summary>
  public IReadOnlyList<string> UniqueVariants { get; } = uniqueVariants;

  /// <summary>
  /// Whether hovering the name should show the variant-name tooltip at all -
  /// false for the common case of a base with neither a Set nor a Unique version,
  /// so those rows never pop an empty tooltip.
  /// </summary>
  public bool HasVariants
    => SetVariants.Count > 0 || UniqueVariants.Count > 0;

  /// <summary>
  /// The user's current rarity selection for this item,
  /// read by <c>MainViewModel</c> when persisting settings.
  /// </summary>
  public RarityFlags SelectedRarities
    => _selectedRarities;

  // --- Selection visibility - whether this base can appear as each rarity at all -----

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

  // Selection state - whether the user has each one selected -----

  public bool IsNormalSelected
    => _selectedRarities.HasFlag(RarityFlags.Normal);
  public bool IsEtherealSocketedSelected
    => _selectedRarities.HasFlag(RarityFlags.EtherealSocketed);
  public bool IsMagicSelected
    => _selectedRarities.HasFlag(RarityFlags.Magic);
  public bool IsRareSelected
    => _selectedRarities.HasFlag(RarityFlags.Rare);
  public bool IsSetSelected
    => _selectedRarities.HasFlag(RarityFlags.Set);
  public bool IsUniqueSelected
    => _selectedRarities.HasFlag(RarityFlags.Unique);
  public bool IsSuperiorSelected
    => _selectedRarities.HasFlag(RarityFlags.Superior);

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
    OnPropertyChanged(nameof(IsNormalSelected));
    OnPropertyChanged(nameof(IsEtherealSocketedSelected));
    OnPropertyChanged(nameof(IsMagicSelected));
    OnPropertyChanged(nameof(IsRareSelected));
    OnPropertyChanged(nameof(IsSetSelected));
    OnPropertyChanged(nameof(IsUniqueSelected));
    OnPropertyChanged(nameof(IsSuperiorSelected));
  }
}
