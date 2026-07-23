using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D2RLootRadar.Domain.Loot;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace D2RLootRadar.Desktop.ViewModels;

/// <summary>
/// Represents a category group in the UI.
/// </summary>
public sealed partial class CategoryViewModel : ObservableObject
{
  /// <summary>
  /// <see cref="IsExpanded"/>'s value from just before a search started, captured once on the empty → non-empty transition.
  /// Restored when the search is cleared, so the user's own manual expand/collapse choices survive a
  /// search round-trip instead of every category being left auto-expanded (or auto-collapsed) once filtering stops.
  /// Null whenever no search is currently available.
  /// </summary>
  private bool? _expandedBeforeSearch;

  /// <summary>
  /// Whether this category should render at all under the current catalog search filter -
  /// false once every item in it has been filtered out,
  /// so a non-matching category disappears entirely instead of showing an always-collapsed,
  /// permanently empty shell.
  /// Always true when no search is active.
  /// </summary>
  [ObservableProperty]
  private bool _isVisible = true;

  [ObservableProperty]
  private bool _isExpanded = false;

  public string Name { get; }

  public ObservableCollection<ItemBaseViewModel> Items { get; }

  /// <summary>
  /// Tri-state selection state for the header checkbox.
  /// false = none, true = all, null = mixed.
  /// Bound with IsThreeState="False" so the user can only click to true/false,
  /// but a null (mixed) value still renders as indeterminate.
  /// </summary>
  public bool? AllSelected
  {
    get
    {
      int selected = Items.Count(i => i.SelectedRarities != RarityFlags.None);
      if (selected == 0)
        return false;
      if (selected == Items.Count)
        return true;

      return null;
    }
    set
    {
      bool select = value ?? true; // indeterminate click → select all

      foreach (ItemBaseViewModel item in Items)
      {
        // Unsubscribe during bulk update to fire one notification, not N.
        item.PropertyChanged -= OnItemPropertyChanged;

        // "Select all" turns on every rarity each item can actually appear as (ItemBaseViewModel.ApplicableRarities),
        // not literally every one of the seven flags.
        item.SetAllRarities(select);

        item.PropertyChanged += OnItemPropertyChanged;
      }

      RaiseSelectionProperties();
    }
  }

  public int SelectedCount
    => Items.Count(i => i.SelectedRarities != RarityFlags.None);

  public bool HasSelection
    => SelectedCount > 0;

  public string CountLabel => SelectedCount > 0
    ? $"{SelectedCount} / {Items.Count}"
    : $"{Items.Count} items";

  public CategoryViewModel(string name, IEnumerable<ItemBaseViewModel> items)
  {
    Name = name;
    Items = new(items);

    foreach (ItemBaseViewModel item in Items)
      item.PropertyChanged += OnItemPropertyChanged;
  }

  /// <summary>
  /// Applies the main windoe's full set of catalogs filters:
  /// updates every item's visibility, hides this while category if nothing in it matches
  /// (or if this category itself isn't one of the selected values in an active Category filter),
  /// and auto-expands it if something does.
  /// 
  /// <para>
  /// An item must satisfy both at once - <see cref="ItemBaseViewModel.MatchesSearch"/> AND
  /// <see cref="ItemBaseViewModel.MatchesTier"/> - matching the main window's stated behavior:
  /// "no search term = filters only (if any), a search term = search AND filters (if any)".
  /// Either one alone already treats "no active constraint" as "every passes"
  /// (blank search text, null tier filter), so that composition falls out for free -
  /// no special-casing needed here for "only one of the two is actually active".
  /// </para>
  /// 
  /// <para>
  /// The Category filter is handled differently from the other three, deliberately - it isn't checker per item at all.
  /// Every item in this <see cref="CategoryViewModel"/> shares the exact same category construction
  /// (that's what <see cref="Name"/> already <em>is</em> -
  /// see <c>MainViewModel.Load</c>'s grouping, so "does this item's category match the filter" is
  /// always the same answer for every item here - one check per category, not one per item.
  /// </para>
  /// 
  /// <para>
  /// On the empty → non-empty transition, the category's current <see cref="IsExpanded"/> is
  /// captured into <see cref="_expandedBeforeSearch"/> before it gets overwritten by the
  /// search's own auto-expand behavior.
  /// On the non-empty → empty transition (search cleared),
  /// that captures value is restored and the field reset to null.
  /// A blank-to-blank or search-to-search call (most keystrokes) touches neither.
  /// </para>
  /// </summary>
  public void ApplyFilters(CatalogFilter filter)
  {
    if (filter.IsActive && _expandedBeforeSearch is null)
      _expandedBeforeSearch = IsExpanded;

    bool anyVisible = false;

    foreach (ItemBaseViewModel item in Items)
    {
      bool matches = item.MatchesSearch(filter.SearchText) &&
                     item.MatchesTier(filter.Tiers) &&
                     item.MatchesVariants(filter.RequireUniqueVariant, filter.RequireSetVariant);
      item.IsVisible = matches;
      anyVisible |= matches;
    }

    bool categoryAllowed = filter.Categories.Count == 0 ||
                           filter.Categories.Contains(Name);
    IsVisible = anyVisible && categoryAllowed;

    if (filter.IsActive)
      IsExpanded = IsVisible; // reveal matches; categories with none are hidden outright anyway
    else if (_expandedBeforeSearch is not null)
    {
      IsExpanded = _expandedBeforeSearch.Value;
      _expandedBeforeSearch = null;
    }
  }

  [RelayCommand]
  private void ToggleExpanded()
    => IsExpanded = !IsExpanded;

  private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs ea)
  {
    if (ea.PropertyName == nameof(ItemBaseViewModel.SelectedRarities))
      RaiseSelectionProperties();
  }

  private void RaiseSelectionProperties()
  {
    OnPropertyChanged(nameof(AllSelected));
    OnPropertyChanged(nameof(SelectedCount));
    OnPropertyChanged(nameof(HasSelection));
    OnPropertyChanged(nameof(CountLabel));
  }
}
