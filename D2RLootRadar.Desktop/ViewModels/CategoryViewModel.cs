using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace D2RLootBeeper.Desktop.ViewModels;

/// <summary>
/// Represents a category group in the UI.
/// </summary>
public sealed partial class CategoryViewModel : ObservableObject
{
  public string Name { get; }

  public ObservableCollection<ItemBaseViewModel> Items { get; }

  [ObservableProperty]
  private bool _isExpanded = false;

  /// <summary>
  /// Tri-state seelction state for the header checkbox.
  /// false = none, true = all, null = mixed.
  /// Bound with IsThreeState="False" so the user can only click to true/false,
  /// but a null (mixed) value still renders as indeterminate.
  /// </summary>
  public bool? AllSelected
  {
    get
    {
      int selected = Items.Count(i => i.IsSelected);
      if (selected == 0) return false;
      if (selected == Items.Count) return true;
      return null;
    }
    set
    {
      bool select = value ?? true; // indeterminate click → select all

      // Unsubscribe during bulk update to fire one notification, not N.
      foreach (ItemBaseViewModel item in Items)
        item.PropertyChanged -= OnItemPropertyChanged;

      foreach (ItemBaseViewModel item in Items)
        item.IsSelected = select;

      foreach (ItemBaseViewModel item in Items)
        item.PropertyChanged += OnItemPropertyChanged;

      RaiseSelectionProperties();
    }
  }

  public int SelectedCount => Items.Count(i => i.IsSelected);

  public bool HasSelection => SelectedCount > 0;

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

  [RelayCommand]
  private void ToggleExpanded()
    => IsExpanded = !IsExpanded;

  private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs ea)
  {
    if (ea.PropertyName == nameof(ItemBaseViewModel.IsSelected))
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
