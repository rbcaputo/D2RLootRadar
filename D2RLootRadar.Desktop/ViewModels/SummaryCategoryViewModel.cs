namespace D2RLootRadar.Desktop.ViewModels;

/// <summary>
/// A read-only snapshot of one category's currently-selected item names,
/// used to render the "currenlty watching" summary panel in the main window.
/// </summary>
public sealed class SummaryCategoryViewModel(string name, IReadOnlyList<string> items)
{
  /// <summary>
  /// The category's display group name, e.g. "Axe" or "Rune".
  /// </summary>
  public string Name { get; } = name;

  /// <summary>
  /// Names of the items selected within this category.
  /// </summary>
  public IReadOnlyList<string> Items { get; } = items;
}
