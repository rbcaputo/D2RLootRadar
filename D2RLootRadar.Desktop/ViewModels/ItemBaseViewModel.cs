using CommunityToolkit.Mvvm.ComponentModel;

namespace D2RLootRadar.Desktop.ViewModels;

/// <summary>
/// Represents a single selectable item base in the watch list UI (a checkbox row).
/// </summary>
public partial class ItemBaseViewModel(
  string name,
  bool isSelected,
  IReadOnlyList<string> setVariants,
  IReadOnlyList<string> uniqueVariants
) : ObservableObject
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

  /// <summary>
  /// Names of Set items that shate this base (e.g. "Light Belt" → "Arctic Binding", "Bane's Authority").
  /// Empty when this base has no Set version.
  /// </summary>
  public IReadOnlyList<string> SetVariants { get; } = setVariants;

  /// <summary>
  /// Names of Unique items that share this base (e.g. "Monarch" → "Stormshield").
  /// Empty when this base has no Unique version.
  /// </summary>
  public IReadOnlyList<string> UniqueVariants { get; } = uniqueVariants;

  /// <summary>
  /// Drives the green dot in the row - true when this base has a Set version.
  /// </summary>
  public bool HasSetVariant => SetVariants.Count > 0;

  /// <summary>
  /// Drives the tan/gold dot in the row - true when this base has a Unique version.
  /// </summary>
  public bool HasUniqueVariant => UniqueVariants.Count > 0;

  /// <summary>
  /// Whether hovering the name should show the variant-name tooltip at all -
  /// false for the common case of a base with neither a Set nor a Unique version,
  /// so those rows never pop an empty tooltip.
  /// </summary>
  public bool HasVariants => HasSetVariant || HasUniqueVariant;
}
