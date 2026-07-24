using System.Windows.Input;

namespace D2RLootRadar.Desktop.ViewModels;

/// <summary>
/// One removable chip in the active-filters row below the search box -
/// a label plus the specific command that clears just that one filter value.
/// 
/// <para>
/// Built fresh by <c>MainViewModel.RefreshActiveFilters</c> every time any filter changes,
/// rather than kept as long-lived state that gets incrementally patched -
/// the whole list is at most a few dozen short-lived records,
/// cheap enough that rebuilding it outright is simpler than diffing it against the previous
/// state to stay in sync.
/// </para>
/// </summary>
public sealed record ActiveFilterTag(string Label, ICommand RemoveCommand);
