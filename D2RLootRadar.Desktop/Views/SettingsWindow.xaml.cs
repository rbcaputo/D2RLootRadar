using D2RLootRadar.Desktop.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace D2RLootRadar.Desktop.Views;

/// <summary>
/// Modal Settings window: alert tone/volume and overlay toggle.
/// Opened as a new instance each time (registered <c>Transient</c> in DI) so it can be
/// closed and reopened freely without hitting WPF's "cannot reuse a closed Window" error.
/// </summary>
public partial class SettingsWindow : Window
{
  private readonly SettingsViewModel _viewModel;

  public SettingsWindow(SettingsViewModel viewModel)
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
