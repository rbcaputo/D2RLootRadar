using D2RLootRadar.Desktop.ViewModels;
using System.ComponentModel;
using System.Windows;

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
}