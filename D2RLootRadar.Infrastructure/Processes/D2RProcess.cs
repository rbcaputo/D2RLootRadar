namespace D2RLootRadar.Infrastructure.Processes;

/// <summary>
/// Known D2R process names, shared by <see cref="Capture.GameCaptureService"/> (window lookup)
/// and <see cref="GameProcessService"/> (running-process check),
/// so the two can never drift out of sync with each other.
/// </summary>
internal static class D2RProcess
{
  internal static readonly string[] Names = [
    "D2R",
    "Diablo II Resurrected"
  ];
}
