using System.Diagnostics;

namespace D2RLootRadar.Infrastructure.Processes;

/// <summary>
/// Locates D2R's main window handle by process name.
/// 
/// Public (unlike <see cref="D2RProcess"/>, which stays internal to this assembly) so
/// <c>D2RLootRadar.Desktop</c> can look up the same window handle -
/// e.g. to compare it against the current foreground window - without duplicating this lookup a third time.
/// </summary>
public static class GameWindowLocator
{
  /// <summary>
  /// Returns D2R's main window handle, or <see cref="IntPtr.Zero"/> if the game isn't
  /// running or hasn't created a main window yet.
  /// </summary>
  public static IntPtr FindWindow()
  {
    foreach (string name in D2RProcess.Names)
    {
      Process[] matches = Process.GetProcessesByName(name);

      try
      {
        if (matches.Length > 0 && matches[0].MainWindowHandle != IntPtr.Zero)
          return matches[0].MainWindowHandle;
      }
      finally
      {
        foreach (Process process in matches)
          process.Dispose();
      }
    }

    return IntPtr.Zero;
  }
}
