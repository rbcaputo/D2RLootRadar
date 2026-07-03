namespace D2RLootRadar.Application.Settings;

/// <summary>
/// Represents persisted user preferences.
/// 
/// Stored in settings.json by the Infrastructure layer.
/// </summary>
public sealed record UserSettings
{
  private double _fuzzyMatchThreshold = 0.80;
  private int _beepFrequencyHz = 800;
  private int _beepDurationMs = 200;
  private int _beepVolume = 10;
  private int _markerDisplaySeconds = 2;

  /// <summary>
  /// Item base names selected by the user. (e.g. "Monarch, "Ber Rune").
  /// </summary>
  public IReadOnlyCollection<string> SelectedItemBases { get; init; } = [];

  /// <summary>
  /// Minimum fuzzy-match similarity score.
  /// Clamped to 0.0-1.0. Default: 0.80 (80% similarity).
  /// </summary>
  public double FuzzyMatchThreshold
  {
    get => _fuzzyMatchThreshold;
    init => _fuzzyMatchThreshold = Math.Clamp(value, 0.0, 1.0);
  }

  /// <summary>
  /// Beep tone frequency in Hz.
  /// Clamped to 100-5000 Hz. Default: 800 Hz.
  /// </summary>
  public int BeepFrequencyHz
  {
    get => _beepFrequencyHz;
    init => _beepFrequencyHz = Math.Clamp(value, 100, 5_000);
  }

  /// <summary>
  /// Duration in milliseconds for the alert beep.
  /// Clamped to 1-5000 ms. Default: 200 ms.
  /// </summary>
  public int BeepDurationMs
  {
    get => _beepDurationMs;
    init => _beepDurationMs = Math.Clamp(value, 1, 5_000);
  }

  /// <summary>
  /// Beep volume as a percentage.
  /// 0 = mute (no playback attempted at all). Default: 10.
  /// </summary>
  public int BeepVolume
  {
    get => _beepVolume;
    init => _beepVolume = Math.Clamp(value, 0, 100);
  }

  /// <summary>
  /// How long a detection marker stays on screen before auto-hiding, in seconds -
  /// or until the next detection, whichever comes firts.
  /// Clamped to 1-10 s. Default: 2 s.
  /// </summary>
  public int MarkerDisplaySeconds
  {
    get => _markerDisplaySeconds;
    init => _markerDisplaySeconds = Math.Clamp(value, 1, 10);
  }

  /// <summary>
  /// Whether the on-screen detection overlay is shown after a match.
  /// </summary>
  public bool OverlayEnabled { get; init; } = true;
}
