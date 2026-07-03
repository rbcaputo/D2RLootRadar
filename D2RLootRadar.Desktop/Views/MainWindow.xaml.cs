using D2RLootRadar.Desktop.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace D2RLootRadar.Desktop.Views;

/// <summary>
/// The main application window: item-base watch list and selection summary.
/// </summary>
public partial class MainWindow : Window
{
  private readonly MainViewModel _viewModel;

  public MainWindow(MainViewModel viewModel)
  {
    InitializeComponent();

    _viewModel = viewModel;
    DataContext = _viewModel;
  }

  /// <summary>
  /// Flushes any pending debounced save immediately, so a close during a pending save never loses a change.
  /// </summary>
  protected override void OnClosing(CancelEventArgs ea)
  {
    _viewModel.FlushSave();
    base.OnClosing(ea);
  }

  /// <summary>
  /// Forces full application shutdown once this window has actually closed -
  /// explicit and unambiguous, rather than relying on ShutdownMode/Application.MainWindow auto-detection,
  /// which can be thrown off by other windows (e.g. OveralyWindow)
  /// having their native handle created before this window is ever shown.
  /// </summary>
  protected override void OnClosed(EventArgs ea)
  {
    base.OnClosed(ea);
    System.Windows.Application.Current.Shutdown();
  }
}