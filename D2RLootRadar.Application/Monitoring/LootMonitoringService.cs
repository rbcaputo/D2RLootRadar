using D2RLootRadar.Application.Contracts;
using D2RLootRadar.Application.Settings;
using D2RLootRadar.Domain.Loot;
using Microsoft.Extensions.Logging;

namespace D2RLootRadar.Application.Monitoring;

/// <summary>
/// Coordinates the full loot detection workflow.
/// 
/// Pipeline: ALT pressed → capture → OCR → fuzzy match → alert.
/// 
/// Triggered exclusively by the global keyboard hook; no polling loop.
/// A re-entry guard ensures that a slow OCR pass does not stack with the next ALT press.
/// </summary>
public sealed class LootMonitoringService : IDisposable
{
  private readonly IKeyboardMonitor _keyboardMonitor;
  private readonly IGameProcessService _gameProcessService;
  private readonly IGameCaptureService _gameCaptureService;
  private readonly IOcrService _ocrService;
  private readonly IAlertService _alertService;
  private readonly IOverlayService _overlayService;
  private readonly ISettingsStore _settingsStore;
  private readonly IItemBaseCatalog _catalog;
  private readonly IFuzzyMatcher _fuzzyMatcher;
  private readonly ILogger<LootMonitoringService> _logger;

  /// <summary>
  /// Interlocked flag: 1 while a detection pass is in progress, 0 while idle.
  /// Prevents concurrent passes when ALT is pressed repeatedly.
  /// </summary>
  private int _isProcessing;

  // 0 = released, 1 = held
  private volatile int _altHeld;

  /// <summary>
  /// Minimum number of capture-OCR passes per ALT press, regardless of hold duration.
  /// Each pass costs ~600 ms (160 ms capture + 440 ms OCR).
  /// Two passes = ~1.2 s worst-case on a miss, but exits immediately on the first successful match.
  /// Guarantees that even a very quick tap (shorter than one pass) still gets a real chance at
  /// detecting a label, since <see cref="_altHeld"/> alone can't be trusted for that.
  /// </summary>
  private const int MinPasses = 2;

  /// <summary>
  /// The exact text of the Superior quality prefix, as D2R renders it.
  /// Shared between <see cref="QualityPrefixes"/> (stripped for matching) and <see cref="HasSuperiorPrefix"/>
  /// (checked for the Superior-only filter) so the literal string exists in exactly one place.
  /// </summary>
  private const string SuperiorPrefix = "Superior";

  /// <summary>
  /// Upper bound on how long a single ALT-triggered pipeline run (all passes combined)
  /// may run before it is cancelled.
  /// Chiefly a safety net for a held ALT key combined with a slow OCR pass -
  /// without it, <see cref="RunPipelineAsync"/>'s while loop could in theory run for
  /// as long as the key stays held.
  /// </summary>
  private static readonly TimeSpan PipelineTimeout = TimeSpan.FromSeconds(10);

  private static readonly HashSet<string> QualityPrefixes
    = new(StringComparer.OrdinalIgnoreCase)
    {
      "Cracked",
      "Crude",
      "Damaged",
      "Low Quality",
      SuperiorPrefix
    };

  /// <summary>
  /// Wires up the keyboard hook's press/release events.
  /// Does not start monitoring - call <see cref="Start"/> explicitly once the host is ready.
  /// </summary>
  public LootMonitoringService(
    IKeyboardMonitor keyboardMonitor,
    IGameProcessService gameProcessService,
    IGameCaptureService gameCaptureService,
    IOcrService ocrService,
    IAlertService alertService,
    IOverlayService overlayService,
    ISettingsStore settingsStore,
    IItemBaseCatalog catalog,
    IFuzzyMatcher fuzzyMatcher,
    ILogger<LootMonitoringService> logger
  )
  {
    _keyboardMonitor = keyboardMonitor;
    _gameProcessService = gameProcessService;
    _gameCaptureService = gameCaptureService;
    _ocrService = ocrService;
    _alertService = alertService;
    _overlayService = overlayService;
    _settingsStore = settingsStore;
    _catalog = catalog;
    _fuzzyMatcher = fuzzyMatcher;
    _logger = logger;

    _keyboardMonitor.AltPressed += OnAltPressed;
    _keyboardMonitor.AltReleased += OnAltReleased;
  }

  /// <summary>
  /// Begins listening for ALT presses via the global keyboard hook.
  /// </summary>
  public void Start()
    => _keyboardMonitor.Start();

  /// <summary>
  /// Stops listening for ALT presses.
  /// Any in-flight pipeline run is left to finish on its own.
  /// </summary>
  public void Stop()
    => _keyboardMonitor.Stop();

  /// <summary>
  /// Strips a leading "Superior"/"Cracked"/"Damaged"/"Low Quality" quality prefix from OCR'd text,
  /// since the catalog store bare base names (e.g. "Phase Blade", not "Superior Phase Blade").
  /// 
  /// Internal rather than private specifically so it can be unit-tested directly
  /// (see <c>D2RLootRadar.Tests/Monitoring/StripQualityPrefixTests.cs</c>) without needing to
  /// construct the full service and its nine dependencies just to exercise a pure string transform.
  /// </summary>
  internal static string StripQualityPrefix(string normalizedText)
  {
    foreach (string prefix in QualityPrefixes)
      if (normalizedText.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase))
        return normalizedText[(prefix.Length + 1)..].Trim();

    return normalizedText;
  }

  /// <summary>
  /// Maps a color-sampled <see cref="LabelRarity"/> to its corresponding single <see cref="RarityFlags"/> bit.
  /// <see cref="LabelRarity.Unknown"/> maps to <see cref="RarityFlags.None"/> -
  /// an inconclusive color sample must never satisfy any selection.
  /// </summary>
  private static RarityFlags ColorFlag(LabelRarity rarity)
    => rarity switch
    {
      LabelRarity.Normal => RarityFlags.Normal,
      LabelRarity.EtherealSocketed => RarityFlags.EtherealSocketed,
      LabelRarity.Magic => RarityFlags.Magic,
      LabelRarity.Rare => RarityFlags.Rare,
      LabelRarity.Set => RarityFlags.Set,
      LabelRarity.Unique => RarityFlags.Unique,
      LabelRarity.RuneMaterial => RarityFlags.RuneMaterial,
      LabelRarity.Shard => RarityFlags.Shard,
      _ => RarityFlags.None
    };

  /// <summary>
  /// Whether a detection satisfies a specific item's selected rarities.
  /// 
  /// True if the detection's sampled label color is one of the selected flags,
  /// OR the selection includes Superior and the detection's text carries the Superior prefix -
  /// these two checks are independent of each other (not both required), since Superior has no label color of its own;
  /// it's checked from text regardless of which color flags are also selected.
  /// 
  /// Internal rather than private so it can be unit-tested directly,
  /// same rationale as <see cref="StripQualityPrefix"/>.
  /// </summary>
  internal static bool IsRarityMatch(DetectionResult detection, RarityFlags selectedRarities)
  {
    RarityFlags detectedColor = ColorFlag(detection.Rarity);

    if (
      detectedColor != RarityFlags.None &&
      selectedRarities.HasFlag(detectedColor)
    ) return true;

    return selectedRarities.HasFlag(RarityFlags.Superior) &&
      HasSuperiorPrefix(detection);
  }

  /// <summary>
  /// Whether a detection's recognized text starts with the "Superior" quality prefix.
  /// Checked against <see cref="DetectionResult.NormalizedText"/>, which <c>OcrService</c> already lowercases
  /// (and, for multi-word lines, collapses internal whitespace on) -
  /// so a plain ordinal-ignore-case prefix check is sufficient here.
  /// </summary>
  private static bool HasSuperiorPrefix(DetectionResult detection)
    => detection.NormalizedText.StartsWith(
      SuperiorPrefix + " ",
      StringComparison.OrdinalIgnoreCase
    );

  /// <summary>
  /// Handles a Left ALT key down.
  /// Marks ALT as held, then starts a pipeline run unless one is already in progress
  /// (the <see cref="_isProcessing"/> guard makes repeated ALT taps during a
  /// slow OCR pass a no-op rather than stacking work).
  /// </summary>
  private async void OnAltPressed(object? sender, EventArgs ea)
  {
    Interlocked.Exchange(ref _altHeld, 1);

    if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0)
      return; // already running

    try
    {
      await RunPipelineAsync();
    }
    catch (Exception ex)
    {
      // async void cannot propagate - surface to debug output.
      _logger.LogError(ex, "Unhandled exception in loot detection pipeline.");
    }
    finally
    {
      Interlocked.Exchange(ref _isProcessing, 0);
    }
  }

  /// <summary>
  /// Handles a Left ALT key-up.
  /// Lets a running pipeline know it may stop after <see cref="MinPasses"/>.
  /// </summary>
  private void OnAltReleased(object? sender, EventArgs ea)
     => Interlocked.Exchange(ref _altHeld, 0);

  /// <summary>
  /// Runs one full ALT-triggered detection pipeline:
  /// waits a frame for D2R to render item labels, then repeatedly captures + OCRs + matches against the
  /// watch list until either a match is found, <see cref="PipelineTimeout"/> elapses,
  /// or the minimum pass count is satisfied and ALT has been released.
  /// Logs and swallows a timeout/cancellation so the player doesn't see a crash if a pass runs unexpectedly long.
  /// </summary>
  private async Task RunPipelineAsync()
  {
    if (!_gameProcessService.IsRunning())
      return;

    // One frame at 60 fps - enough for D2R to render labels, short enough to survive a quick tap.
    await Task.Delay(16);

    // Hard timeout: the whole pipeline run (all passes combined) must complete within PipelineTimeout,
    // primarily to bound a held ALT key against a run of slow OCR passes.
    using CancellationTokenSource cTokenSource = new(PipelineTimeout);
    CancellationToken cToken = cTokenSource.Token;

    // Load current settings + build watch list.
    // Re-read on every pass so UI changes take effect immediately.
    UserSettings settings = _settingsStore.Load();
    WatchList watchList = BuildWatchList(settings);
    if (watchList.Items.Count == 0)
      return;

    try
    {
      await RunDetectionLoopAsync(settings, watchList, cToken);
    }
    catch (OperationCanceledException) when (cToken.IsCancellationRequested)
    {
      _logger.LogWarning(
        "Loot detection pipeline timed out after {Timeout}.",
        PipelineTimeout
      );
    }
  }

  /// <summary>
  /// The capture → OCR → match loop itself, factored out of <see cref="RunPipelineAsync"/> so
  /// the timeout/cancellation handling stays in one place.
  /// </summary>
  private async Task RunDetectionLoopAsync(
    UserSettings settings,
    WatchList watchList,
    CancellationToken cToken
  )
  {
    int pass = 0;

    while (pass < MinPasses || _altHeld == 1)
    {
      pass++;

      CaptureFrame capture
        = await _gameCaptureService.CaptureAsync(cToken);
      if (capture.IsEmpty)
        continue;

      IReadOnlyCollection<DetectionResult> detections
        = await _ocrService.DetectAsync(capture.ImageData, cToken);
      if (detections.Count == 0)
        continue;

      // One marker per distinct matched item name -
      // first matching detection for that item wins its on-screen position.
      Dictionary<string, DetectionMarker> markers
        = new(StringComparer.OrdinalIgnoreCase);

      foreach (DetectionResult result in detections)
      {
        string candidate = StripQualityPrefix(result.NormalizedText);

        foreach (WatchedItem item in watchList.Items)
        {
          double similarity = _fuzzyMatcher.Similarity(candidate, item.Base.Name);
          if (
            similarity >= settings.FuzzyMatchThreshold &&
            IsRarityMatch(result, item.SelectedRarities)
          )
          {
            if (!markers.ContainsKey(item.Base.Name))
            {
              int screenX = capture.WindowBounds.X + result.BoundingBox.CenterX;
              int screenY = capture.WindowBounds.Y + result.BoundingBox.CenterY;
              markers[item.Base.Name] = new(item.Base.Name, screenX, screenY);
            }

            break;
          }
        }
      }

      if (markers.Count > 0)
      {
        // Pass the already-loaded settings through instead of having AlertService re-read settings.json -
        // this pipeline run has already pais that cost once.
        await _alertService.AlertAsync(settings, cToken);
        _overlayService.ShowMarkers([.. markers.Values]);

        return;
      }
    }
  }

  /// <summary>
  /// Resolves the user's per-item selections against the full catalog to produce the set of items to
  /// actively match against this pass, each paired with its selected rarities.
  /// Case-insensitive because the stored selection and the catalog names may differ only in casing.
  /// Selections are intersected against each item's current <c>ApplicableRarities</c> -
  /// a bit saved under an older catalog version that no longer applies to this item
  /// (e.g. a stale Normal flag on a base later reclassified to RuneMaterial) is dropped rather than
  /// silently kept as an unmatchable, UI-invisible selection.
  /// An item left with <see cref="RarityFlags.None"/> after that intersection is treated as not watched -
  /// there's no separate "is this item selected" flag.
  /// </summary>
  private WatchList BuildWatchList(UserSettings settings)
  {
    Dictionary<string, RarityFlags> selections
      = new(settings.ItemRaritySelections, StringComparer.OrdinalIgnoreCase);
    IEnumerable<WatchedItem> items = _catalog.GetAll()
      .Select(i => (Base: i, Selected: selections.GetValueOrDefault(i.Name) & i.ApplicableRarities))
      .Where(x => x.Selected != RarityFlags.None)
      .Select(x => new WatchedItem(x.Base, x.Selected));

    return new(items);
  }

  /// <summary>
  /// Unsubscribes from the keyboard hook's events.
  /// Does not dispose the hook itself - it is a DI-owned singleton with its own lifetime.
  /// </summary>
  public void Dispose()
  {
    _keyboardMonitor.AltPressed -= OnAltPressed;
    _keyboardMonitor.AltReleased -= OnAltReleased;
    // Do NOT call _keyboardMonitor.Dispose() - DI owns its lifetime
  }
}
