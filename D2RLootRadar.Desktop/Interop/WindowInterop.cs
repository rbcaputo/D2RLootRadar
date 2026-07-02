using System.Runtime.InteropServices;

namespace D2RLootRadar.Desktop.Interop;

/// <summary>
/// Win32 declarations that make the overlay window a true passive overlay:
/// it never steals keyboard focus from the game, and never appears in Alt+Tab or the taskbar.
/// </summary>
internal static class WindowInterop
{
  private const int GWL_EXSTYLE = -20;
  private const int WS_EX_NOACTIVATE = 0x08000000;
  private const int WS_EX_TOOLWINDOW = 0x00000080;
  private const int WS_EX_TRANSPARENT = 0x00000020;
  private const int WS_EX_LAYERED = 0x00080000;

  [DllImport("user32.dll")]
  private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

  [DllImport("user32.dll")]
  private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

  /// <summary>
  /// WS_EX_NOACTIVATE: the window can never receive keyboard focus - D2R is never interrupted.
  /// WS_EX_TOOLWINDOW: hides the window from Alt+Tab and the taskbar.
  /// WS_EX_TRANSPARENT + WS_EX_LAYERED: all mouse input passes straight through to
  /// whatever is beneath - the overlay cannot be clicked or dragged.
  /// </summary>
  internal static void ApplyOverlayStyles(IntPtr hwnd)
  {
    int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
    SetWindowLong(
      hwnd,
      GWL_EXSTYLE,
      exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_LAYERED
    );
  }
}
