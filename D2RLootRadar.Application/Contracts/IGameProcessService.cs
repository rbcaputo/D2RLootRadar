namespace D2RLootRadar.Application.Contracts;

/// <summary>
/// Provides access to D2R process status.
/// </summary>
public interface IGameProcessService
{
  /// <summary>
  /// Returns true if D2R is currently running.
  /// </summary>
  bool IsRunning();
}
