using D2RLootRadar.Application.Monitoring;
using D2RLootRadar.Domain.Loot;

namespace D2RLootRadar.Tests.Monitoring;

public class RarityScoreTests
{
  private static DetectionResult Detection(
    string rawText,
    LabelRarity rarity,
    double rarityConfidence = 1.0,
    string? normalizedTextOverride = null
  ) => new(
    rawText,
    normalizedTextOverride ?? rawText.ToLowerInvariant(),
    Confidence: 0.95f,
    BoundingBox: default,
    rarity,
    rarityConfidence
  );

  // --- Full-confidence cases -----
  // A unanimous vote (confidence 1.0) collapses RarityScore back down to a 0/1 veredict.


  [Theory]
  [InlineData(LabelRarity.Normal, RarityFlags.Normal)]
  [InlineData(LabelRarity.EtherealSocketed, RarityFlags.EtherealSocketed)]
  [InlineData(LabelRarity.Magic, RarityFlags.Magic)]
  [InlineData(LabelRarity.Rare, RarityFlags.Rare)]
  [InlineData(LabelRarity.Set, RarityFlags.Set)]
  [InlineData(LabelRarity.Unique, RarityFlags.Unique)]
  public void RarityScore_ConfidentColorMatchesSelectedFlag_ReturnsOne(
    LabelRarity detectedColor,
    RarityFlags selected
  )
  {
    DetectionResult detection = Detection("Monarch", detectedColor);

    Assert.Equal(1.0, LootMonitoringService.RarityScore(detection, selected));
  }

  [Fact]
  public void RarityScore_ConfidentColorNotAmongSelectedFlags_ReturnsZero()
  {
    DetectionResult detection = Detection("Monarch", LabelRarity.Magic);
    RarityFlags selected = RarityFlags.Unique | RarityFlags.Set;

    Assert.Equal(0.0, LootMonitoringService.RarityScore(detection, selected));
  }

  [Fact]
  public void RarityScore_UnknownColor_AlwaysZeroRegadlessOfSelectionOrConfidence()
  {
    // An inconclusive color sample must never contribute a positive score,
    // regadless of how broad the user's selection is - it carries no evidence either way.
    DetectionResult detection = Detection("Monarch", LabelRarity.Unknown, rarityConfidence: 1.0);
    RarityFlags selected = RarityFlags.Normal |
      RarityFlags.EtherealSocketed |
      RarityFlags.Magic |
      RarityFlags.Rare |
      RarityFlags.Set |
      RarityFlags.Unique |
      RarityFlags.Superior;

    Assert.Equal(0.0, LootMonitoringService.RarityScore(detection, selected));
  }

  [Fact]
  public void RarityScore_SuperiorSelectedAndPrefixPresent_ReturnsOneRegardlessOfColor()
  {
    // Superior is text-based and independent of color - a gray Superior item should match on
    // the Superior flag alone even if EtherealSocketed itself isn't selected.
    DetectionResult detection = Detection(
      "Superior Phase Blade",
      LabelRarity.EtherealSocketed,
      normalizedTextOverride: "superior phase blade"
    );

    Assert.Equal(1.0, LootMonitoringService.RarityScore(detection, RarityFlags.Superior));
  }

  [Fact]
  public void RarityScore_SuperiorSelectedButNoPrefixInText_ReturnsZero()
  {
    DetectionResult detection = Detection("Phase Blade", LabelRarity.Normal);

    Assert.Equal(0.0, LootMonitoringService.RarityScore(detection, RarityFlags.Superior));
  }

  [Fact]
  public void RarityScore_SuperiorPresentBuNotSelected_DoesNotScoreOnPrefixAlone()
  {
    // The text does carry "Superior ", but the user only asked for Unique -
    // the color (gray, not tan) doesn't satisfy that either.
    DetectionResult detection = Detection(
      "Superior Phase Blade",
      LabelRarity.EtherealSocketed,
      normalizedTextOverride: "superior phase blade"
    );

    Assert.Equal(0.0, LootMonitoringService.RarityScore(detection, RarityFlags.Unique));
  }

  [Fact]
  public void RarityScore_MultipleFlagsSelected_MatchesOnAnyOne()
  {
    DetectionResult detection = Detection("Ring", LabelRarity.Rare);
    RarityFlags selected = RarityFlags.Magic | RarityFlags.Rare;

    Assert.Equal(1.0, LootMonitoringService.RarityScore(detection, selected));
  }

  [Fact]
  public void RarityScore_NoneSelected_AlwaysZero()
  {
    DetectionResult detection = Detection("Monarch", LabelRarity.Unique);

    Assert.Equal(0.0, LootMonitoringService.RarityScore(detection, RarityFlags.None));
  }

  // --- Blended-confidence cases -----
  // The actual point of RarityScore over the old boolean gate:
  // a knife's-edge vote no longer collapses to the same flat 0 or 1 a landslide vote would.

  [Fact]
  public void RarityScore_AmbiguousWrongColor_ScoresNearHalfNotZero()
  {
    // Votes barely favored Magic over the selected Rare - RarityScore.confidence 0.5 -
    // this is "could easily have gone the other way", not a confident mismatch,
    // so it shouldn't collapse all the way down to 0 the way a landslide-wrong vote would.
    DetectionResult detection = Detection("Ring", LabelRarity.Magic, rarityConfidence: 0.5);

    Assert.Equal(0.5, LootMonitoringService.RarityScore(detection, RarityFlags.Rare));
  }

  [Fact]
  public void RarityScore_ConfidentWrongColor_ScoredsNearZero()
  {
    // A landslide vote for the wrong tier is real evidence against the match, and should score close to 0 -
    // clearly distinct from the ambiguous case above.
    DetectionResult detection = Detection("Ring", LabelRarity.Magic, rarityConfidence: 0.95);

    Assert.Equal(0.05, LootMonitoringService.RarityScore(detection, RarityFlags.Rare), precision: 10);
  }

  [Fact]
  public void RarityScore_AmbiguousRightColor_ScoresLowerThanConfidentRightColor()
  {
    DetectionResult ambiguous = Detection("Ring", LabelRarity.Rare, rarityConfidence: 0.55);
    DetectionResult confident = Detection("Ring", LabelRarity.Rare, rarityConfidence: 1.0);

    double ambiguousScore = LootMonitoringService.RarityScore(ambiguous, RarityFlags.Rare);
    double confidentScore = LootMonitoringService.RarityScore(confident, RarityFlags.Rare);

    Assert.Equal(0.55, ambiguousScore);
    Assert.Equal(1.0, confidentScore);
    Assert.True(ambiguousScore < confidentScore);
  }
}
