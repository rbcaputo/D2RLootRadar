using System.Runtime.InteropServices;

namespace D2RLootRadar.Infrastructure.Capture;

/// <summary>
/// Win32 API declarations required for game window capture.
/// Kept separate from Input/NativeMethods to avoid mixing concerns.
/// </summary>
internal static class Win32Methods
{
  /// <summary>
  /// Instructs PrintWindow to composite the window's DirectX swap-chain content, not just the legacy GDI surface.
  /// Required for D2R.
  /// </summary>
  internal const uint PW_RENDERFULLCONTENT = 0x00000002;

  /// <summary>
  /// Win32 RECT layout: left/top/right/bottom in screen pixel coordinates.
  /// </summary>
  [StructLayout(LayoutKind.Sequential)]
  internal struct RECT
  {
    public int Left, Top, Right, Bottom;
  }

  /// <summary>
  /// Retrieves the bounding rectangle of the window identified by <paramref name="hWnd"/>,
  /// in screen coordinates.
  /// </summary>
  [DllImport("user32.dll", SetLastError = true)]
  internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

  /// <summary>
  /// Renders the window identified by <paramref name="hwnd"/> into the device context <paramref name="hdcBlt"/>.
  /// </summary>
  [DllImport("user32.dll", SetLastError = true)]
  internal static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
}
