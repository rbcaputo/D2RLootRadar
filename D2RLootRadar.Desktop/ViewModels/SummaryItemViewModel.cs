using D2RLootRadar.Domain.Loot;

namespace D2RLootRadar.Desktop.ViewModels;

/// <summary>
/// A read-only snapshot of one watched item's name and selected rarities,
/// used to render the "currently watching" summary pannel.
/// 
/// Unlike <see cref="ItemBaseViewModel"/>'s rarities, these reflect only which rarities are
/// currently selected (not which are merely applicable) -
/// the summary shows what you're actually watching for, not the full set of options.
/// </summary>
public sealed class SummaryItemViewModel(string name, RarityFlags selectedRarities)
{
  public string Name { get; } = name;

  public bool ShowNormal
    => selectedRarities.HasFlag(RarityFlags.Normal);
  public bool ShowEtherealSocketed
    => selectedRarities.HasFlag(RarityFlags.EtherealSocketed);
  public bool ShowMagic
    => selectedRarities.HasFlag(RarityFlags.Magic);
  public bool ShowRare
    => selectedRarities.HasFlag(RarityFlags.Rare);
  public bool ShowSet
    => selectedRarities.HasFlag(RarityFlags.Set);
  public bool ShowUnique
    => selectedRarities.HasFlag(RarityFlags.Unique);
  public bool ShowSuperior
    => selectedRarities.HasFlag(RarityFlags.Superior);
}
