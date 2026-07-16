using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
  /// All item base categories, in display order, each with its selectable items.
  /// </summary>
  public ObservableCollection<CategoryViewModel> Categories { get; } = [];

  /// <summary>
  /// The non-empty subset of <see cref="Categories"/>, used to render the "currently watching" summary pannel.
  /// </summary>
  public ObservableCollection<SummaryCategoryViewModel> Summary { get; } = [];

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

  // --- Load -----

  /// <summary>
  /// Builds <see cref="Categories"/> from the full catalog,
  /// pre-checking whichever items are in the user's saved selection, then populates the initial summary.
  /// </summary>
  private void Load()
  {
    IsGameRunning = _gameProcessService.IsRunning();

    UserSettings settings = _settingsStore.Load();
    Dictionary<string, RarityFlags> selections
      = new(settings.ItemRaritySelections, StringComparer.OrdinalIgnoreCase);

    // Group by DisplayGroup (e.g. "Axe", "Sword", "Circlet", "Rune", "Key").
    // Sort by domain category first so related slots appear together, then alphabetically within each category group.
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
          x.MaxSockets,
          x.SetVariants,
          x.UniqueVariants
        ));

      CategoryViewModel category = new(group.Key, items);
      category.PropertyChanged += OnCategoryPropertyChanged;

      _categoryOrder[category.Name] = Categories.Count;

      Categories.Add(category);
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
    IReadOnlyList<string> selected = [
      .. category.Items
        .Where(i => i.SelectedRarities != RarityFlags.None)
        .Select(i => i.Name)
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
