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
}