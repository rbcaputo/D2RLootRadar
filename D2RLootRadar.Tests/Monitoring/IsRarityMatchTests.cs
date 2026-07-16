using D2RLootRadar.Application.Monitoring;
using D2RLootRadar.Domain.Loot;

namespace D2RLootRadar.Tests.Monitoring;

public class IsRarityMatchTests
{
  private static DetectionResult Detection(
    string rawText,
    LabelRarity rarity,
    string? normalizedTextOverride = null
  ) => new(
    rawText,
    normalizedTextOverride ?? rawText.ToLowerInvariant(),
    Confidence: 0.95f,
    BoundingBox: default,
    rarity
  );

  [Theory]
  [InlineData(LabelRarity.Normal, RarityFlags.Normal)]
  [InlineData(LabelRarity.EtherealSocketed, RarityFlags.EtherealSocketed)]
  [InlineData(LabelRarity.Magic, RarityFlags.Magic)]
  [InlineData(LabelRarity.Rare, RarityFlags.Rare)]
  [InlineData(LabelRarity.Set, RarityFlags.Set)]
  [InlineData(LabelRarity.Unique, RarityFlags.Unique)]
  public void IsRarityMatch_ColorMatchesSelectedFlag_ReturnsTrue(
    LabelRarity detectedColor,
    RarityFlags selected
  )
  {
    DetectionResult detection = Detection("Monarch", detectedColor);

    Assert.True(LootMonitoringService.IsRarityMatch(detection, selected));
  }

  [Fact]
  public void IsRarityMatch_ColorNotAmongSelectedFlags_ReturnsFalse()
  {
    DetectionResult detection = Detection("Monarch", LabelRarity.Magic);
    RarityFlags selected = RarityFlags.Unique | RarityFlags.Set;

    Assert.False(LootMonitoringService.IsRarityMatch(detection, selected));
  }

  [Fact]
  public void IsRarityMatch_UnknownColor_NeverMatchesEvenIfEverythingSelected()
  {
    // An inconclusive color sample must never be treated as a match,
    // regardless of how broad the user's selection is.
    DetectionResult detection = Detection("Monarch", LabelRarity.Unknown);
    RarityFlags selected = RarityFlags.Normal |
      RarityFlags.EtherealSocketed |
      RarityFlags.Magic |
      RarityFlags.Rare |
      RarityFlags.Set |
      RarityFlags.Unique |
      RarityFlags.Superior;

    Assert.False(LootMonitoringService.IsRarityMatch(detection, selected));
  }

  [Fact]
  public void IsRarityMatch_SuperiorSelectedAndPrefixPresent_ReturnsTrueRegardlessOfColor()
  {
    // Superior is text-based and independent of color - a gray Superior item should match on
    // the Superior flag alone even if EtherealSocketed itself isn't selected.
    DetectionResult detection = Detection(
      "Superior Phase Blade",
      LabelRarity.EtherealSocketed,
      normalizedTextOverride: "superior phase blade"
    );

    Assert.True(LootMonitoringService.IsRarityMatch(detection, RarityFlags.Superior));
  }

  [Fact]
  public void IsRarityMatch_SuperiorSelectedButNoPrefixInText_ReturnsFalse()
  {
    DetectionResult detection = Detection("Phase Blade", LabelRarity.Normal);

    Assert.False(LootMonitoringService.IsRarityMatch(detection, RarityFlags.Superior));
  }

  [Fact]
  public void IsRarityMatch_SuperiorPresentButNotSelected_DoesNotMatchOnPrefixAlone()
  {
    // The text does carry "Superior ", but the user only asked for Unique -
    // the color (gray, not tan) doesn't satisfy that either.
    DetectionResult detection = Detection(
      "Superior Phase Blade",
      LabelRarity.EtherealSocketed,
      normalizedTextOverride: "superior phase blade"
    );

    Assert.False(LootMonitoringService.IsRarityMatch(detection, RarityFlags.Unique));
  }

  [Fact]
  public void IsRarityMatch_MultipleFlagsSelected_MatchesOnAnyOne()
  {
    DetectionResult detection = Detection("Ring", LabelRarity.Rare);
    RarityFlags selected = RarityFlags.Magic | RarityFlags.Rare;

    Assert.True(LootMonitoringService.IsRarityMatch(detection, selected));
  }

  [Fact]
  public void IsRarityMatch_NoneSelected_NeverMatches()
  {
    DetectionResult detection = Detection("Monarch", LabelRarity.Unique);

    Assert.False(LootMonitoringService.IsRarityMatch(detection, RarityFlags.None));
  }
}
