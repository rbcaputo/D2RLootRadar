using D2RLootRadar.Application.Settings;

namespace D2RLootRadar.Application.Contracts;

/// <summary>
/// Loads and saves user settings.
/// </summary>
public interface ISettingsStore
{
  /// <summary>
  /// Reads settings from disk, or returns defaults if no settings file exists yet (e.g. first run).
  /// Implementations should never throw for a missing file.
  /// </summary>
  UserSettings Load();

  /// <summary>
  /// Persists the given settings to disk, overwriting any previous file.
  /// </summary>
  void Save(UserSettings settings);
}
