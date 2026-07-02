using CommunityToolkit.Mvvm.ComponentModel;

namespace D2RLootRadar.Desktop.ViewModels;

/// <summary>
/// Represents a single selectable item base in the watch list UI (a checkbox row).
/// </summary>
public partial class ItemBaseViewModel(string name, bool isSelected) : ObservableObject
{
  /// <summary>
  /// The item base name, matching the catalog and used as the OCR match target.
  /// </summary>
  public string Name { get; } = name;

  /// <summary>
  /// Whether the user has this item base checked for active monitoring.
  /// </summary>
  [ObservableProperty]
  private bool _isSelected = isSelected;
}
