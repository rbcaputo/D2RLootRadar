using D2RLootRadar.Application.Monitoring;
using D2RLootRadar.Application.Settings;
using D2RLootRadar.Domain.Loot;

namespace D2RLootRadar.Tests.Monitoring;

public class FilterByModeTests
{
  private static DetectionResult MakeDetection(
    string rawText,
    LabelRarity rarity = LabelRarity.Other,
    string? normalizedTextOverride = null
  ) => new(
         rawText,
         normalizedTextOverride ?? rawText.ToLowerInvariant(),
         Confidence: 0.95f,
         BoundingBox: default,
         rarity
       );

  [Fact]
  public void FilterByMode_All_ReturnsDetectionsUnchanged()
  {
    DetectionResult[] detections = [
      MakeDetection("Flawless Skull"),
      MakeDetection("Ring"),
      MakeDetection("Amulet", LabelRarity.Unique),
      MakeDetection("Superior Phase Blade")
    ];

    IEnumerable<DetectionResult> result
      = LootMonitoringService.FilterByMode(detections, DetectionMode.All);

    Assert.Equal(detections, result);
  }

  [Fact]
  public void FilterByMode_UniqueOnly_KeepsUniqueOnlyRarity()
  {
    DetectionResult[] detections = [
      MakeDetection("Small Charm", LabelRarity.Other),
      MakeDetection("Ring", LabelRarity.Other),
      MakeDetection("Superior Phase Blade", LabelRarity.Other),
      MakeDetection("Shako", LabelRarity.Unique)
    ];

    DetectionResult[] result
      = [.. LootMonitoringService.FilterByMode(detections, DetectionMode.UniqueOnly)];
    DetectionResult onlyResult = Assert.Single(result);

    Assert.Equal("Shako", onlyResult.RawText);
  }

  [Fact]
  public void FilterByMode_UniqueOnly_ExcludesUnknownRarity()
  {
    // An inconclusive color sample must never be treated as a match.
    DetectionResult[] detections = [
      MakeDetection("Crossbow", LabelRarity.Unknowm)
    ];

    IEnumerable<DetectionResult> result
      = LootMonitoringService.FilterByMode(detections, DetectionMode.UniqueOnly);

    Assert.Empty(result);
  }

  [Fact]
  public void FilterByMode_SuperiorOnly_KeepsOnlyTextWithSuperiorPrefix()
  {
    DetectionResult[] detections = [
      MakeDetection("Phase Blade"),
      MakeDetection("Ring", LabelRarity.Other),
      MakeDetection("Shako", LabelRarity.Unique),
      MakeDetection("Superior Monarch")
    ];

    DetectionResult[] result
      = [.. LootMonitoringService.FilterByMode(detections, DetectionMode.SuperiorOnly)];
    DetectionResult onlyResult = Assert.Single(result);

    Assert.Equal("Superior Monarch", onlyResult.RawText);
  }

  [Fact]
  public void FilterByMode_SuperiorOnly_IsCaseInsensitive()
  {
    // OcrService lowercases NormalizedText, but the filter shouldn't depend on that happening upstream -
    // it should be correct on its own terms.
    DetectionResult[] detections = [
      MakeDetection("SUPERIOR ARCHON PLATE", normalizedTextOverride: "superior archon plate")
    ];

    IEnumerable<DetectionResult> result
      = LootMonitoringService.FilterByMode(detections, DetectionMode.SuperiorOnly);

    Assert.Single(result);
  }

  [Fact]
  public void FilterByMode_SuperiorOnly_RequiresTrailingSpace_NotJustPrefix()
  {
    // A word that merely starts with the same letters ("Superiorman") must not match -
    // "Superior" has to be a whole standalone word, not a substring.
    DetectionResult[] detections = [
      MakeDetection("Superiorman")
    ];

    IEnumerable<DetectionResult> result
      = LootMonitoringService.FilterByMode(detections, DetectionMode.SuperiorOnly);

    Assert.Empty(result);
  }

  [Fact]
  public void FilterByMode_SuperiorOnly_StandaloneSuperiorWord_DoesNotMatch()
  {
    // A lone "Superior" token (no item name attached) can't tell us which item it modifies,
    // so it must not count as a match on its own.
    DetectionResult[] detections = [
      MakeDetection("Superior")
    ];

    IEnumerable<DetectionResult> result
      = LootMonitoringService.FilterByMode(detections, DetectionMode.SuperiorOnly);

    Assert.Empty(result);
  }

  [Fact]
  public void FilterByMode_SuperiorOnly_NoSuperiorItemsPresent_ReturnsEmpty()
  {
    DetectionResult[] detections = [
      MakeDetection("Small Charm"),
      MakeDetection("Ring", LabelRarity.Other)
    ];

    IEnumerable<DetectionResult> result
      = LootMonitoringService.FilterByMode(detections, DetectionMode.SuperiorOnly);

    Assert.Empty(result);
  }
}
