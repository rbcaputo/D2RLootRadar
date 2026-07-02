using D2RLootRadar.Application.Settings;

namespace D2RLootRadar.Application.Contracts;

/// <summary>
/// Notifies the user when a watched item has been detected.
/// </summary>
public interface IAlertService
{
  /// <summary>
  /// Plays the alert tone using the user's persisted settings, which are loaded from disk by this call.
  /// Prefer the <see cref="AlertAsync(UserSettings, CancellationToken)"/> overload when the
  /// caller has already loaded settings this pass, to avoid a redundant read.
  /// </summary>
  Task AlertAsync(CancellationToken cToken);

  /// <summary>
  /// Playes the alert tone using an already-loaded <see cref="UserSettings"/> instance,
  /// skipping the settings-store read.
  /// Called by the detection pipeline, which loads settings once per pipeline run for watch-list
  /// resolution and can reuse that copy here.
  /// </summary>
  Task AlertAsync(UserSettings settings, CancellationToken cToken);

  /// <summary>
  /// Plays a short test tone with the given parameters, ignoring persisted settings.
  /// Used by the Settings window to preview changes before saving.
  /// </summary>
  Task PlayTestSoundAsync(int frequencyHz, int volumePercent, CancellationToken cToken);
}
