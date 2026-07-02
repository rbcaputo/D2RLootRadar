using D2RLootRadar.Application.Contracts;
using System.Diagnostics;

namespace D2RLootRadar.Infrastructure.Processes;

/// <summary>
/// Detects whether D2R is currently running.
/// </summary>
public sealed class GameProcessService : IGameProcessService
{
  /// <summary>
  /// Known D2R process names.
  /// 
  /// We support multiple names because Blizzard occasionally changes launchers.
  /// </summary>
  private static readonly string[] ProcessNames
    = D2RProcess.Names;

  /// <inheritdoc />
  public bool IsRunning()
  {
    foreach (string name in ProcessNames)
    {
      Process[] processes = Process.GetProcessesByName(name);

      try
      {
        if (processes.Length > 0)
          return true;
      }
      finally
      {
        foreach (Process process in processes)
          process.Dispose();
      }
    }

    return false;
  }
}
