using D2RLootRadar.Desktop.Views;
using System.Windows;

namespace D2RLootRadar.Desktop.Services;

/// <summary>
/// Owns the system tray icon and its right-click menu (Show / Exit).
/// Keeps the application reachable while the main window is hidden,
/// and shows a one-time balloon hint the first time the window is minimized to the tray,
/// so the behavior doesn't look like the application silently vanished.
/// </summary>
public sealed class TrayIconService : IDisposable
{
  private readonly NotifyIcon _notifyIcon;
  
  private MainWindow? _mainWindow;
  private bool _hasShownMinimizeHint;
  
  /// <summary>
  /// Creates the tray icon immediately, visisible for the apps's entire runtime -
  /// not just while the main window is hidden - matching the convention of most
  /// tray-resident apps of always offering quick tray access.
  /// </summary>
  public TrayIconService()
  {
    _notifyIcon = new()
    {
      Icon = ExtractAppIcon(),
      Text = "D2RLootRadar",
      Visible = true
    };
    _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();

    ContextMenuStrip menu = new();
    menu.Items.Add("Show", null, (_, _) => ShowMainWindow());
    menu.Items.Add("Exit", null, (_, _) => _mainWindow?.ExitApplication());

    _notifyIcon.ContextMenuStrip = menu;
  }

  /// <summary>
  /// Associates this tray icon with the main window, and starts watching its
  /// visibility so the one-time hint fires regardless of what hid it.
  /// </summary>
  public void AttachTo(MainWindow mainWindow)
  {

    _mainWindow = mainWindow;
    mainWindow.IsVisibleChanged += (_, ea) =>
    {
      if (ea.NewValue is false)
        ShowMinimizeHintOnce();
    };
  }

  private void ShowMinimizeHintOnce()
  {
    if (_hasShownMinimizeHint)
      return;

    _notifyIcon.ShowBalloonTip(
      timeout: 3000,
      tipTitle: "D2RLootRadar is still running",
      tipText: "Detection continues in the background. Right-click this icon to reopen or exit.",
      tipIcon: ToolTipIcon.Info
    );

    _hasShownMinimizeHint = true;
  }

  private void ShowMainWindow()
  {
    if (_mainWindow is null)
      return;

    _mainWindow.Show();
    _mainWindow.WindowState = WindowState.Normal; // in case it was minimized before being hidden
    _mainWindow.Activate();
  }

  /// <summary>
  /// Reuses the running executable's own icon rather than requiring a dedicated .ico asset.
  /// Falls back to the generic system applicationn icon if extraction fails for any reason
  /// (e.g. running from an unusual host during development).
  /// </summary>
  private static Icon ExtractAppIcon()
  {
    string? path = Environment.ProcessPath;

    return path is not null
      ? Icon.ExtractAssociatedIcon(path) ?? SystemIcons.Application
      : SystemIcons.Application;
  }

  /// <summary>
  /// Hides and disposes the tray icon so it doesn't linger as a "ghost" icon in the tray after the app exits -
  /// a well-known NotifyfIcon gotcha if Dispose isn't called before the process ends.
  /// </summary>
  public void Dispose()
  {
    _notifyIcon.Visible = false;
    _notifyIcon.Dispose();
  }
}
