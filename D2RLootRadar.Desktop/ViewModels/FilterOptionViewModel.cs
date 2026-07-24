using CommunityToolkit.Mvvm.ComponentModel;

namespace D2RLootRadar.Desktop.ViewModels;

/// <summary>
/// One checkable row in a multi-select filter group (Tier, Cateogry) - 
/// a name plus whther it's currently selected.
/// 
/// <para>
/// Deliberately the same small shape for both groups rather than Tier getting its own hand-written
/// named bool properties (<c>IsNormalTierSelected</c>, etc.).
/// Using the same mechanism for both means the popup only needs one binding pattern
/// (an <c>ItemsControl</c> over a collection of these) instead of two different ones for what
/// are conceptually the same kind of control.
/// </para>
/// 
/// <para>
/// <see cref="MainViewModel"/> owns the actual selection sets (<c>_selectedTiers</c>/<c>_selectedCategories</c>) -
/// toggling <see cref="IsSelected"/> here doesn't do any filtering itself,
/// it just raises <see cref="ObservableObject.PropertyChanged"/>, which MainViewModel liestens for
/// (same wiring pattern <see cref="CategoryViewModel"/> already uses for its own child
/// <see cref="ItemBaseViewModel"/> rows) to keep its own set in sync and re-apply filters.
/// </para>
/// </summary>
public sealed partial class FilterOptionViewModel(string name) : ObservableObject
{
  public string Name { get; } = name;

  [ObservableProperty]
  private bool _isSelected;
}
