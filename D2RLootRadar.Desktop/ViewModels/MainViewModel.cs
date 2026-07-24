using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D2RLootRadar.Application.Catalog;
using D2RLootRadar.Application.Contracts;
using D2RLootRadar.Application.Settings;
using D2RLootRadar.Desktop.Views;
using D2RLootRadar.Domain.Loot;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;

namespace D2RLootRadar.Desktop.ViewModels;

/// <summary>
/// Backs the main window:
/// the full item-base watch list grouped by category, the running selection summary, and D2R process status.
/// 
/// <para>
/// <strong>Auto-save:</strong>
/// selection changes debounce through <see cref="ScheduleSave"/> the same way <see cref="SettingsViewModel"/> does,
/// so rapid checkbox toggling collapses into a single disk write.
/// </para>
/// </summary>
public partial class MainViewModel : ObservableObject
{
  private readonly IItemBaseCatalog _catalog;
  private readonly ISettingsStore _settingsStore;
  private readonly IGameProcessService _gameProcessService;
  private readonly IServiceProvider _serviceProvider;
  private readonly DispatcherTimer _processTimer;
  private readonly DispatcherTimer _saveTimer;

  /// <summary>
  /// Each category's position in <see cref="Categories"/>' display order, keyed by name.
  /// Built once in <see cref="Load"/> - Categories is never reordered afterward,
  /// so this stays valid for the app's whole filetime.
  /// Lets <see cref="FindInsertionIndex"/> keep <see cref="Summary"/> in the same order without
  /// re-deriving it from Categories every time.
  /// </summary>
  private readonly Dictionary<string, int> _categoryOrder = [];

  /// <summary>
  /// Backing sets for the Tier/Category filter groups -
  /// <see cref="TierFilterOptions"/>/<see cref="CategoryFilterOptions"/> for the checkable rows that stay in sync with these,
  /// and <see cref="OnFilterOptionChanged"/> for how a checkbox toggle updtaes them.
  /// Plain <see cref="HashSet{T}"/>, not an observable collection -
  /// nothing binds to these <see cref="ActiveFilters"/>, both of which are refreshed explicitly on
  /// every change rather than relying on collection-change notifications from these sets themselves.
  /// </summary>
  private readonly HashSet<string> _selectedTiers = [];
  private readonly HashSet<string> _selectedCategories = [];

  /// <summary>
  /// Whether D2R is currently running, polled every 3 seconds via <see cref="_processTimer"/>.
  /// </summary>
  [ObservableProperty]
  private bool _isGameRunning;

  /// <summary>
  /// Total number of item bases currently selected across all categories.
  /// </summary>
  [ObservableProperty]
  private int _totalSelectedCount;

  /// <summary>
  /// True until <see cref="CompleteWarmup"/> is called once, by <see cref="Views.MainWindow"/> after it
  /// has forced a real layout pass on every category's vritualized item list.
  /// 
  /// <para>
  /// Categories start collapsed (<see cref="CategoryViewModel.IsExpanded"/> = false),
  /// so their VrtualizingStackPanel (see the inner ItemsControl under CategoryViewModel's DataTemplate in
  /// MainWindow.xaml) has never been measured an none of its row containers - each with a Popup-based
  /// rarity picker and info tooltip - exist yet.
  /// Any filter change - search text, a Tier/Category checkbox, or a variant toggle - runs through
  /// <see cref="OnFiltersChanged"/> and expands every matching category at once (<see cref="CategoryViewModel.ApplyFilters"/>),
  /// which would be the first thing to trigger that container build-out for most categories simultaneously,
  /// all in one synchronous layout pass - which would cause a stutter on the very first search,
  /// checkbox toggle, or manual expand.
  /// 
  /// <para>
  /// Drives a simple loading veil over the category list (MainWindow.xaml) so that one-time cost is
  /// paid visibly and up front as startup, instead of silently during the user's first intercation.
  /// </para>
  /// </para>
  /// </summary>
  [ObservableProperty]
  private bool _isWarmingUp = true;

  /// <summary>
  /// Current catalog search term, bound to the search box.
  /// Filtering itself is cheap (plain substring containment over ~600 items),
  /// so it's applied synchronously on every keystroke via <see cref="OnSearchTextChanged"/> rather than
  /// debounced the way <see cref="ScheduleSave"/> debounces disk writes.
  /// </summary>
  [ObservableProperty]
  private string _searchText = string.Empty;

  /// <summary>
  /// Whether the catalog should be narrowed to bases with at least one Unique variant.
  /// See <see cref="ItemBaseViewModel.MatchesVariants"/> when both are on at once.
  /// </summary>
  [ObservableProperty]
  private bool _requireUniqueVariant;

  /// <summary>
  /// Whether the catalog should be narrowed to bases with at least one Set variant.
  /// </summary>
  [ObservableProperty]
  private bool _requireSetVariant;

  /// <summary>
  /// Whether the Tier/Category/Variant filter popup is currently open.
  /// Pure UI state - never persisted, same treatment as <see cref="ItemBaseViewModel.IsPopupOpen"/>.
  /// </summary>
  [ObservableProperty]
  private bool _isFilterPopupOpen;

  /// <summary>
  /// All item base categories, in display order, each with its selectable items.
  /// </summary>
  public ObservableCollection<CategoryViewModel> Categories { get; } = [];

  /// <summary>
  /// The non-empty subset of <see cref="Categories"/>, used to render the "currently watching" summary pannel.
  /// </summary>
  public ObservableCollection<SummaryCategoryViewModel> Summary { get; } = [];

  /// <summary>
  /// The three Tier checkboxes in the filter popup - <see cref="FilterOptionViewModel"/>'s remarks for
  /// why Tier uses the same mechanism as <see cref="CategoryFilterOptions"/> rather than
  /// named per-value properties.
  /// </summary>
  public ObservableCollection<FilterOptionViewModel> TierFilterOptions { get; } = [];

  /// <summary>
  /// One checkbox per distinct category (e.g. "Axe", "Orb", "Grimoire") in the filter popup -
  /// built from <see cref="Categories"/> in <see cref="Load"/>,
  /// so it's always exactly the set of category groups actually present in the catalog,
  /// with no separate list to keep in sync by hand.
  /// </summary>
  public ObservableCollection<FilterOptionViewModel> CategoryFilterOptions { get; } = [];

  /// <summary>
  /// Onde removable chip per currently-active filter value (search text included),
  /// shown in a row below the search bar.
  /// Rebuilt from scratch on every filter change by <see cref="RefreshActiveFilters"/> - 
  /// see <see cref="ActiveFilterTag"/>'s remarks for why that's simples than incrementally
  /// patching this collection to match.
  /// </summary>
  public ObservableCollection<ActiveFilterTag> ActiveFilters { get; } = [];

  /// <summary>
  /// Whether a search is currently active - drives the clear ("×") button's visibility.
  /// </summary>
  public bool HasSearchText
    => !string.IsNullOrEmpty(SearchText);

  /// <summary>
  /// Whether any popup-driven filter (Tier, Category, or either variant toggle) is currently active -
  /// drives the filter button's highlighted state.
  /// Deliberately doesn't include <see cref="SearchText"/>, which already has its own visible state
  /// (text in the box, plus its own clear button) - folding it in here too would juest be a second,
  /// redundant signal for the same thing.
  /// </summary>
  public bool HasActiveFilters
    => _selectedTiers.Count > 0 ||
       _selectedCategories.Count > 0 ||
       RequireUniqueVariant ||
       RequireSetVariant;

  /// <summary>
  /// How many popup-driven filter values are currently selected, across Tier/Category/variant toggles -
  /// shown as a badge count on the filters button.
  /// Deliberately excludes <see cref="SearchText"/>, for the same reason <see cref="HasActiveFilters"/> does.
  /// </summary>
  public int ActiveFilterCount
    => _selectedTiers.Count +
       _selectedCategories.Count +
       (RequireUniqueVariant ? 1 : 0) +
       (RequireSetVariant ? 1 : 0);

  /// <summary>
  /// Whether <see cref="ActiveFilters"/> has anything in it -
  /// collapses the tags row entirely rather then showing an empty strip when nothing is filtered.
  /// Manually raised wherever <see cref="ActiveFilters"/> is rebuilt, same as every other computed
  /// property in this class that depends on a collection rather than a single backing field.
  /// </summary>
  public bool HasActiveFilterTags
    => ActiveFilters.Count > 0;

  /// <summary>
  /// Loads the catalog and the user's saved selection, then starts the game-status poll timer.
  /// </summary>
  public MainViewModel(
    IItemBaseCatalog catalog,
    ISettingsStore settingsStore,
    IGameProcessService gameProcessService,
    IServiceProvider serviceProvider
  )
  {
    _catalog = catalog;
    _settingsStore = settingsStore;
    _gameProcessService = gameProcessService;
    _serviceProvider = serviceProvider;

    _saveTimer = new()
    {
      Interval = TimeSpan.FromMilliseconds(400)
    };
    _saveTimer.Tick += (_, _) =>
    {
      _saveTimer.Stop();
      ExecuteSave();
    };

    _processTimer = new()
    {
      Interval = TimeSpan.FromSeconds(3)
    };
    _processTimer.Tick += (_, _)
      => IsGameRunning = _gameProcessService.IsRunning();
    _processTimer.Start();

    Load();
  }

  // --- Commands -----

  /// <summary>
  /// Opens the modal Settings window (alert tone/volume, overlay toggle).
  /// </summary>
  [RelayCommand]
  private void OpenSettings()
  {
    SettingsWindow window
      = _serviceProvider.GetRequiredService<SettingsWindow>();
    window.ShowDialog();
  }

  /// <summary>
  /// Clears the search box, restoring every category's pre-search expand state.
  /// Backs the search box's clear ("×") button.
  /// </summary>
  [RelayCommand]
  private void ClearSearch()
    => SearchText = string.Empty;

  // --- Filtering -----

  /// <summary>
  /// Re-applies the catalog filters to every category whenever <see cref="SearchText"/> changes,
  /// and refreshes <see cref="HasSearchText"/> for the clear button.
  /// </summary>
  partial void OnSearchTextChanged(string value)
  {
    OnFiltersChanged();
    OnPropertyChanged(nameof(HasSearchText));
  }

  /// <summary>
  /// Re-applies every filter whenever <see cref="RequireUniqueVariant"/> changes.
  /// </summary>
  partial void OnRequireUniqueVariantChanged(bool value)
    => OnFiltersChanged();

  /// <summary>
  /// Re-applies every filter whenever <see cref="RequireSetVariant"/> changes.
  /// </summary>
  partial void OnRequireSetVariantChanged(bool value)
    => OnFiltersChanged();

  /// <summary>
  /// Reacts to any Tier or Category checkbox toggling by updating the matching backing set
  /// (<see cref="_selectedTiers"/> or <see cref="_selectedCategories"/>) and re-applying filters.
  /// One shared handler for both groups - <see cref="TierFilterOptions"/> and <see cref="CategoryFilterOptions"/>
  /// are both wired to it in <see cref="Load"/>, and <paramref name="targetSet"/> says which set a
  /// given row's group actually belongs to.
  /// </summary>
  private void OnFilterOptionChanged(
    object? sender,
    PropertyChangedEventArgs ea,
    HashSet<string> targetSet
  )
  {
    if (
      ea.PropertyName != nameof(FilterOptionViewModel.IsSelected) ||
      sender is not FilterOptionViewModel option
    ) return;

    if (option.IsSelected)
      targetSet.Add(option.Name);
    else
      targetSet.Remove(option.Name);

    OnFiltersChanged();
  }

  /// <summary>
  /// Fans the current filter state out to every category as one combined pass, refreshes <see cref="HasActiveFilters"/>,
  /// and rebuilds <see cref="ActiveFilters"/> to match -
  /// the one method every filter-changing hook above funnels through,
  /// so nothing has to separately remember to do all three.
  /// </summary>
  private void OnFiltersChanged()
  {
    CatalogFilter filter
      = new(SearchText, _selectedTiers, _selectedCategories, RequireUniqueVariant, RequireSetVariant);

    foreach (CategoryViewModel category in Categories)
      category.ApplyFilters(filter);

    OnPropertyChanged(nameof(HasActiveFilters));
    OnPropertyChanged(nameof(ActiveFilterCount));
    RefreshActiveFilters(filter);
  }

  private void RefreshActiveFilters(CatalogFilter filter)
  {
    ActiveFilters.Clear();

    if (!string.IsNullOrWhiteSpace(filter.SearchText))
      ActiveFilters.Add(new($"Search: {filter.SearchText}", ClearSearchCommand));

    foreach (FilterOptionViewModel option in TierFilterOptions.Where(o => o.IsSelected))
      ActiveFilters.Add(new(option.Name, new RelayCommand(() => option.IsSelected = false)));
    foreach (FilterOptionViewModel option in CategoryFilterOptions.Where(o => o.IsSelected))
      ActiveFilters.Add(new(option.Name, new RelayCommand(() => option.IsSelected = false)));

    if (RequireUniqueVariant)
      ActiveFilters.Add(new("Has Unique", new RelayCommand(() => RequireUniqueVariant = false)));
    if (RequireSetVariant)
      ActiveFilters.Add(new("Has Set", new RelayCommand(() => RequireSetVariant = false)));

    OnPropertyChanged(nameof(HasActiveFilterTags));
  }

  // --- Auto-Save -----

  /// <summary>
  /// Restarts the 400 ms debounce timer so rapid selection changes collapse into a single disk write.
  /// </summary>
  private void ScheduleSave()
  {
    _saveTimer.Stop();
    _saveTimer.Start();
  }

  /// <summary>
  /// Recomputes the selected item-base names from the live UI state and persists them.
  /// </summary>
  private void ExecuteSave()
  {
    Dictionary<string, RarityFlags> selections = Categories
      .SelectMany(c => c.Items)
      .Where(i => i.SelectedRarities != RarityFlags.None)
      .ToDictionary(i => i.Name, i => i.SelectedRarities);

    UserSettings current = _settingsStore.Load();
    _settingsStore.Save(current with
    {
      ItemRaritySelections = selections
    });
  }

  /// <summary>
  /// Cancels any pending debounce and saves immediately.
  /// Called by MainWindow.OnClosing so no change is ever lost.
  /// </summary>
  public void FlushSave()
  {
    _saveTimer.Stop();
    ExecuteSave();
  }

  /// <summary>
  /// Marks item-container warm-up as finished, hiding the loading veil.
  /// Called exactly onde, by <see cref="MainWindow"/>'s WarmUpItemContainersAsync,
  /// after it has forced a real layout pass on every category.
  /// See <see cref="IsWarmingUp"/>.
  /// </summary>
  public void CompleteWarmUp()
    => IsWarmingUp = false;

  // --- Load -----

  /// <summary>
  /// Builds <see cref="Categories"/> from the full catalog,
  /// pre-checking whichever items are in the user's saved selection,
  /// then populates the initial summary.
  /// </summary>
  private void Load()
  {
    IsGameRunning = _gameProcessService.IsRunning();

    UserSettings settings = _settingsStore.Load();
    Dictionary<string, RarityFlags> selections
      = new(settings.ItemRaritySelections, StringComparer.OrdinalIgnoreCase);

    // Group by DisplayGroup (e.g. "Axe", "Sword", "Circlet", "Rune", "Key").
    // Sort by domain category first so related slots appear together,
    // then alphabetically within each category group.
    IOrderedEnumerable<IGrouping<string, ItemBase>> groups = _catalog.GetAll()
      .GroupBy(x => x.DisplayGroup)
      .OrderBy(g => CategoryOrder(g.First().Category))
      .ThenBy(g => g.Key);

    foreach (IGrouping<string, ItemBase> group in groups)
    {
      IEnumerable<ItemBaseViewModel> items = group
        .OrderBy(x => x.Name)
        .Select(x => new ItemBaseViewModel(
          x.Name,
          x.ApplicableRarities,
          selections.GetValueOrDefault(x.Name, RarityFlags.None),
          x.Tier,
          x.MaxSockets,
          x.SetVariants,
          x.UniqueVariants
        ));

      CategoryViewModel category = new(group.Key, items);
      category.PropertyChanged += OnCategoryPropertyChanged;

      _categoryOrder[category.Name] = Categories.Count;

      Categories.Add(category);
    }

    // Fixed three values - see ItemBaseViewModel.MatchesTier's remarks for why exactly these three.
    string[] tiers = [
      "Normal",
      "Exceptional",
      "Elite"
    ];

    foreach (string tier in tiers)
    {
      FilterOptionViewModel option = new(tier);
      option.PropertyChanged += (sender, ea)
        => OnFilterOptionChanged(sender, ea, _selectedTiers);
      TierFilterOptions.Add(option);
    }

    // One row per category actually present in the loaded catalog, in the same display order as Categories itself -
    // no separate list of category names to keep in sync by hand.
    foreach (CategoryViewModel category in Categories)
    {
      FilterOptionViewModel option = new(category.Name);
      option.PropertyChanged += (sender, ea)
        => OnFilterOptionChanged(sender, ea, _selectedCategories);
      CategoryFilterOptions.Add(option);
    }

    RefreshFullSummary();
  }

  /// <summary>
  /// Controls the order in which category groups appear in the UI.
  /// 
  /// Runes lead (most commonly watched), then weapons by sub-type,
  /// then armor slots top-to-bottom, then jewelry and collectibles.
  /// </summary>
  private static int CategoryOrder(ItemCategory category)
    => category switch
    {
      ItemCategory.Rune => 0,
      ItemCategory.Weapon => 1,
      ItemCategory.Helmet => 2,
      ItemCategory.Torso => 3,
      ItemCategory.Shield => 4,
      ItemCategory.Belt => 5,
      ItemCategory.Boots => 6,
      ItemCategory.Gloves => 7,
      ItemCategory.Ring => 8,
      ItemCategory.Amulet => 9,
      ItemCategory.Charm => 10,
      ItemCategory.Jewel => 11,
      ItemCategory.Gem => 12,
      ItemCategory.Material => 13,
      _ => 99
    };

  /// <summary>
  /// Reacts to any category's selection count changing by incrementally updating just that
  /// category's summary entry and scheduling a debounced save.
  /// </summary>
  private void OnCategoryPropertyChanged(object? sender, PropertyChangedEventArgs ea)
  {
    if (
      ea.PropertyName == nameof(CategoryViewModel.SelectedCount) &&
      sender is CategoryViewModel category
    )
    {
      RefreshSummary(category);
      ScheduleSave();
    }
  }

  /// <summary>
  /// Build <see cref="Summary"/> and <see cref="TotalSelectedCount"/> from scratch.
  /// Only used once, from <see cref="Load"/> - every subsequent change goes through the incremental
  /// <see cref="RefreshSummary(CategoryViewModel)"/> instead, since rebuilding all
  /// ~600 item's worth of state on every single checkbox toggle is wasted work,
  /// and <see cref="ObservableCollection{T}.Clear"/> forces the summary model panel to fully
  /// regenerate rather than update the one row that changed.
  /// </summary>
  private void RefreshFullSummary()
  {
    Summary.Clear();

    int total = 0;

    foreach (CategoryViewModel category in Categories)
    {
      if (category.SelectedCount == 0)
        continue;

      Summary.Add(BuildSummaryEntry(category));

      total += category.SelectedCount;
    }

    TotalSelectedCount = total;
  }

  /// <summary>
  /// Updates just the one category's entry in <see cref="Summary"/> -
  /// added, replaced in place, or removed, depending on its current <see cref="CategoryViewModel.SelectedCount"/> -
  /// instead of rebuilding the whole summary from scratch on every single toggle.
  /// </summary>
  private void RefreshSummary(CategoryViewModel category)
  {
    int existingIndex = FindSummaryIndex(category.Name);
    int previousCount = existingIndex >= 0
      ? Summary[existingIndex].Items.Count
      : 0;

    if (category.SelectedCount == 0)
    {
      if (existingIndex >= 0)
        Summary.RemoveAt(existingIndex);
    }
    else if (existingIndex >= 0)
      Summary[existingIndex] = BuildSummaryEntry(category); // same slot - no reordering needed
    else
      Summary.Insert(FindInsertionIndex(category.Name), BuildSummaryEntry(category));

    TotalSelectedCount += category.SelectedCount - previousCount;
  }

  private static SummaryCategoryViewModel BuildSummaryEntry(CategoryViewModel category)
  {
    IReadOnlyList<SummaryItemViewModel> selected = [
      .. category.Items
        .Where(i => i.SelectedRarities != RarityFlags.None)
        .Select(i => new SummaryItemViewModel(i.Name, i.SelectedRarities))
    ];

    return new(category.Name, selected);
  }

  private int FindSummaryIndex(string categoryName)
  {
    for (int i = 0; i < Summary.Count; i++)
      if (Summary[i].Name == categoryName)
        return i;

    return -1;
  }

  /// <summary>
  /// Finds where a newly-non-empty category's summary entry belongs,
  /// so <see cref="Summary"/> always mirrors <see cref="Categories"/>' display order
  /// (Rune, then weapons, then armor, ...) even though entries are inserted one at a time
  /// rather than rebuilt in order every time.
  /// 
  /// <para>
  /// <c>_categoryOrder</c> is populated for every category unconditionally in <see cref="Load"/>,
  /// so a missing entry here shouldn't be reachable -
  /// but a UI action like a checkbox click should never be able to crash the app over a stale/missing lookup,
  /// so an unrecognized name falls back to <see cref="int.MaxValue"/> (appended at the end) rather than throwing.
  /// </para>
  /// </summary>
  private int FindInsertionIndex(string categoryName)
  {
    int position
      = _categoryOrder.GetValueOrDefault(categoryName, int.MaxValue);
    int insertAt = 0;

    while (
      insertAt < Summary.Count &&
      _categoryOrder[Summary[insertAt].Name] < position
    ) insertAt++;

    return insertAt;
  }
}
