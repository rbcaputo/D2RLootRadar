using D2RLootRadar.Domain.Loot;

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
  /// Which rarities the user wants alerts for, per watched item base name (e.g. "Monarch" → Unique).
  /// An item absent from this map, or present with <see cref="RarityFlags.None"/>, is not wacthed at all -
  /// there is no separate "is this item selected" flag;
  /// having at least one rarity selected is what makes an item watched.
  /// </summary>
  public IReadOnlyDictionary<string, RarityFlags> ItemRaritySelections { get; init; }
    = new Dictionary<string, RarityFlags>();

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
