using D2RLootRadar.Desktop.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace D2RLootRadar.Desktop.Views;

/// <summary>
/// The main application window: item-base watch list and selection summary.
/// 
/// Closing this window via the X button does not exit the app -
/// it minimizes to the system tray instead, so loot detection keeps running in the background.
/// Use <see cref="ExitApplication"/> (wired to the tray icon's "Exit" menu item) to
/// actualy terminate the application.
/// </summary>
public partial class MainWindow : Window
{
  private readonly MainViewModel _viewModel;

  private bool _isExiting;

  public MainWindow(MainViewModel viewModel)
  {
    InitializeComponent();

    _viewModel = viewModel;
    DataContext = _viewModel;

    // Fire-and-forget: warms up virtualized item containers behind the loading veil
    // (MainViewModel.IsWarmingUp) once the window has a real layout to measure against.
    // See WarmUpItemContainersAsync for why this exists.
    Loaded += async (_, _) => await WarmUpItemContainersAsync();
  }

  /// <summary>
  /// Requests a genuine, full application exit - bypasses the minimize-to-tray behavior in <see cref="OnClosing"/>.
  /// Called by <see cref="Services.TrayIconService"/>'s "Exit" menu item.
  /// </summary>
  public void ExitApplication()
  {
    _isExiting = true;

    Close();
  }

  /// <summary>
  /// Itercepts the X button:
  /// cancels the close and hides the window instead, so the app keeps runnig in the tray.
  /// Only allows a real close when <see cref="ExitApplication"/> requested it explicitly.
  /// </summary>
  protected override void OnClosing(CancelEventArgs ea)
  {
    if (!_isExiting)
    {
      ea.Cancel = true;

      Hide();

      return;
    }

    _viewModel.FlushSave();
    base.OnClosing(ea);
  }

  /// <summary>
  /// Forces full application shutdown once this window has actually closed -
  /// explicit and unambiguous, rather than relying on ShutdownMode/Application.MainWindow auto-detection,
  /// which can be thrown off by other windows (e.g. OveralyWindow)
  /// having their native handle created before this window is ever shown.
  /// 
  /// Only reached via <see cref="ExitApplication"/>, since <see cref="OnClosing"/>
  /// otherwise intercepts and hides instead of closing.
  /// </summary>
  protected override void OnClosed(EventArgs ea)
  {
    base.OnClosed(ea);
    System.Windows.Application.Current.Shutdown();
  }

  /// <summary>
  /// Forces every category's virtualized row <c>ItemsControl</c> (see the DataTemplate for
  /// <see cref="CategoryViewModel"/> in MainWindow.xaml) to realize its containers once,
  /// right after the window loads, instead of leaving that cost for whichever user action
  /// (a catalog search, a Tier/Category/variant filter checkbox, or manually expanding a category)
  /// happens to touch each one first.
  /// 
  /// <para>
  /// Categories start collapsed, so their VirtualizingStackPanel has never been measured and none of its
  /// row containers - each carrying a Popup-based rarity picker and info tooltip - exist yet.
  /// Any filter change funnels through <see cref="MainViewModel.OnFiltersChanged"/> into
  /// <see cref="CategoryViewModel.ApplyFilters"/>, which auto-expands every matching category at once -
  /// so the first filter interaction of any kind was previously the first thing to trigger that whole
  /// container build-out, for most categories simultaneously, in one synchronous layout pass.
  /// That's the stutter this warm-up removes.
  /// </para>
  /// 
  /// <para>
  /// Expanding a category just long enough for one real <see cref="UIElement.UpdateLayout"/> pass,
  /// the collapsing it back, pays that cost here instead, behind the veil bound to <see cref="MainViewModel.IsWarmingUp"/>.
  /// Yielding to the dispatcher between categories keeps the window responsive to input/paint messages rather
  /// than freezing for one long stretch, and keeps this from racing the very first filter interaction if
  /// the user acts immediately on launch.
  /// </para>
  /// 
  /// <para>
  /// A category the user had already expanded before this runs (not currently possible at startup,
  /// but harmless either way) is left expanded rather than forece-collapsed.
  /// </para>
  /// </summary>
  private async Task WarmUpItemContainersAsync()
  {
    foreach (CategoryViewModel category in _viewModel.Categories)
    {
      bool wasExpanded = category.IsExpanded;
      if (!wasExpanded)
        category.IsExpanded = true;

      UpdateLayout();

      if (!wasExpanded)
        category.IsExpanded = false;

      await Dispatcher.Yield(DispatcherPriority.Background);
    }

    _viewModel.CompleteWarmUp();
  }
}