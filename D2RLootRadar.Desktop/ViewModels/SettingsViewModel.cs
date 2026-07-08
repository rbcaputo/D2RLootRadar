using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D2RLootRadar.Application.Contracts;
using D2RLootRadar.Application.Settings;
using System.Windows.Threading;

namespace D2RLootRadar.Desktop.ViewModels;

/// <summary>
/// Backs the Settings window: alert tone, alert volume, overlay, and detection preferences.
/// 
/// <para>
/// <strong>Auto-save:</strong>
/// every property change (e.g. a slider drag) restarts a 400 ms debounce timer via
/// <see cref="ScheduleSave"/>, so persistence only happens once the user pauses,
/// not on every intermediate value while dragging.
/// </para>
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
  private readonly ISettingsStore _settingsStore;
  private readonly IAlertService _alertService;
  private readonly IOverlayService _overlayService;
  private readonly DispatcherTimer _saveTimer;

  /// <summary>
  /// Alert volume as a percentage (0-100).
  /// 0 mutes the alert entirely.
  /// </summary>
  [ObservableProperty]
  private int _beepVolume;

  /// <summary>
  /// Alert tone frequency in Hz.
  /// </summary>
  [ObservableProperty]
  private int _beepFrequencyHz;

  /// <summary>
  /// Length of the alert tone in milliseconds.
  /// </summary>
  [ObservableProperty]
  private int _beepDurationMs;

  /// <summary>
  /// Minimum fuzzy-match similarity (0.0-1.0) for a detected text token to count as a match.
  /// </summary>
  [ObservableProperty]
  private double _fuzzyMatchThreshold;

  /// <summary>
  /// How long a detection marker stays on screen before auto-hiding, in seconds.
  /// </summary>
  [ObservableProperty]
  private int _markerDisplaySeconds;

  /// <summary>
  /// Backing field for the three mutually-exclusive detection-mode radio buyttons bellow.
  /// Not itself an <c>[ObservableProperty]</c> - each radio button raises change notifications for all three,
  /// since selecting one always deselects the other two.
  /// </summary>
  private DetectionMode _detectionMode;

  /// <summary>
  /// Radio button: no filtering, every watch-list match alerts (current/default behavior).
  /// </summary>
  public bool IsDetectAllSelected
  {
    get => _detectionMode == DetectionMode.All;
    set => SetDetectionMode(value, DetectionMode.All);
  }

  /// <summary>
  /// Radio button: only tan/gold (Unique) labels are matched against the watch list.
  /// </summary>
  public bool IsDetectUniqueOnlySelected
  {
    get => _detectionMode == DetectionMode.UniqueOnly;
    set => SetDetectionMode(value, DetectionMode.UniqueOnly);
  }

  /// <summary>
  /// Radio button: only labels with a "Superior" text prefix are matched against the watch list.
  /// </summary>
  public bool IsDetectSuperiorOnlySelected
  {
    get => _detectionMode == DetectionMode.SuperiorOnly;
    set => SetDetectionMode(value, DetectionMode.SuperiorOnly);
  }

  /// <summary>
  /// Whether the on-screen detection overlay is shown after a match.
  /// </summary>
  [ObservableProperty]
  private bool _overlayEnabled;

  /// <summary>
  /// Loads current settings from disk to seed the bound properties.
  /// </summary>
  public SettingsViewModel(
    ISettingsStore settingsStore,
    IAlertService alertService,
    IOverlayService overlayService
  )
  {
    _settingsStore = settingsStore;
    _alertService = alertService;
    _overlayService = overlayService;

    _saveTimer = new()
    {
      Interval = TimeSpan.FromMilliseconds(400)
    };
    _saveTimer.Tick += (_, _) =>
    {
      _saveTimer.Stop();
      ExecuteSave();
    };

    UserSettings settings = _settingsStore.Load();

    _beepVolume = settings.BeepVolume;
    _beepFrequencyHz = settings.BeepFrequencyHz;
    _beepDurationMs = settings.BeepDurationMs;
    _fuzzyMatchThreshold = settings.FuzzyMatchThreshold;
    _markerDisplaySeconds = settings.MarkerDisplaySeconds;
    _detectionMode = settings.Mode;
    _overlayEnabled = settings.OverlayEnabled;
  }

  /// <summary>
  /// Backs all three radio-button properties above.
  /// A radio button only ever raises this with <paramref name="isChecked"/> = true
  /// (WPF doesn't fire the setter for the one being unchecked),
  /// so unconditionally adopting <paramref name="mode"/> is correct - no need to handle the false case.
  /// </summary>
  private void SetDetectionMode(bool isChecked, DetectionMode mode)
  {
    if (!isChecked || _detectionMode == mode)
      return;

    _detectionMode = mode;

    OnPropertyChanged(nameof(IsDetectAllSelected));
    OnPropertyChanged(nameof(IsDetectUniqueOnlySelected));
    OnPropertyChanged(nameof(IsDetectSuperiorOnlySelected));

    ScheduleSave();
  }

  // --- Commands -----

  /// <summary>
  /// Plays a short (300 ms) preview of the currently-configured tone and volume,
  /// so the user can audition changes before they're persisted.
  /// </summary>
  [RelayCommand]
  private Task TestSoundAsync()
    => _alertService.PlayTestSoundAsync(
         BeepFrequencyHz,
         BeepVolume,
         CancellationToken.None
    );

  // --- CommunityTookit-generated hooks -----
  // Each OnX partial method is invoked automatically by the [ObservableProperty]
  // source generator whenever the corresponding property changes.

  partial void OnBeepVolumeChanged(int value)
    => ScheduleSave();

  partial void OnBeepFrequencyHzChanged(int value)
    => ScheduleSave();

  partial void OnBeepDurationMsChanged(int value)
    => ScheduleSave();

  partial void OnFuzzyMatchThresholdChanged(double value)
    => ScheduleSave();

  partial void OnMarkerDisplaySecondsChanged(int value)
  {
    _overlayService.SetMarkerDisplaySeconds(value);
    ScheduleSave();
  }

  partial void OnOverlayEnabledChanged(bool value)
  {
    _overlayService.SetEnabled(value);
    ScheduleSave();
  }

  // --- Auto-save -----

  /// <summary>
  /// Restarts the 400 ms debounce timer.
  /// Called on every bound property change so that rapid successive edits
  /// (e.g. dragging a slider) collapse into a single disk write.
  /// </summary>
  private void ScheduleSave()
  {
    _saveTimer.Stop();
    _saveTimer.Start();
  }

  /// <summary>
  /// Re-reads the current settings from disk and writes back only the fields this view model owns,
  /// so concurrent changes made elsewhere (e.g. the watch list in <see cref="MainViewModel"/>
  /// are never clobbered by a stale in-memory copy.
  /// </summary>
  private void ExecuteSave()
  {
    UserSettings current = _settingsStore.Load();
    _settingsStore.Save(current with
    {
      BeepVolume = BeepVolume,
      BeepFrequencyHz = BeepFrequencyHz,
      BeepDurationMs = BeepDurationMs,
      FuzzyMatchThreshold = FuzzyMatchThreshold,
      MarkerDisplaySeconds = MarkerDisplaySeconds,
      Mode = _detectionMode,
      OverlayEnabled = OverlayEnabled
    });
  }

  /// <summary>
  /// Cancels any pending debounce and saves immediately.
  /// Called by SettingsWindow.OnClosing so slider changes are never lost.
  /// </summary>
  public void FlushSave()
  {
    _saveTimer.Stop();
    ExecuteSave();
  }
}
