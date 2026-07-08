using D2RLootRadar.Domain.Loot;
using D2RLootRadar.Infrastructure.Ocr;

namespace D2RLootRadar.Tests.Ocr;

public class LabelRarityClassifierTests
{
  // RGB values below are the measured average glyph color from real in-game label captures for
  // each of the six D2R tiers - not synthetic "ideal" colors,
  // so these tests double as a regression guard on the calibration itself.
  // Only Unique should classify as Unique; every other tier -
  // regardless of how different they look from each other - must land on Other.

  [Theory]
  [InlineData(230, 230, 230)] // white (Normal)
  [InlineData(157, 157, 153)] // gray (Ethereal/Socket)
  [InlineData(140, 140, 232)] // blue (Magic)
  [InlineData(49, 222, 47)] // green (Set)
  [InlineData(242, 241, 121)] // yellow (Rare)
  public void Classify_NonUniqueTier_ReturnsOthers(byte r, byte g, byte b)
  {
    LabelRarity result = LabelRarityClassifier.Classify(r, g, b);

    Assert.Equal(LabelRarity.Other, result);
  }

  [Fact]
  public void Classify_UniqueTanGold_ReturnsUnique()
  {
    LabelRarity result = LabelRarityClassifier.Classify(199, 187, 149);

    Assert.Equal(LabelRarity.Unique, result);
  }

  [Theory]
  [InlineData(0, 0, 0)] // pure black - not a real label color
  [InlineData(255, 0, 255)] // magenta - not a real label color
  public void Classify_UnrecognizedColor_ReturnsOtherNotUnknown(byte r, byte g, byte b)
  {
    // The classifier always makes a definite Unique/Other call given a real RGB triple -
    // Unknown is reserved for callers that couldn't sample a color at all.
    LabelRarity result = LabelRarityClassifier.Classify(r, g, b);

    Assert.Equal(LabelRarity.Other, result);
  }

  [Fact]
  public void Classify_UniqueAndRare_DoNotCollideDespiteSharedHueFamily()
  {
    // The one genuinely close pair - both live in the ~45-65 degree hue band.
    // This test exists specifically to guard the saturation-based split between them.
    LabelRarity unique = LabelRarityClassifier.Classify(199, 187, 149);
    LabelRarity rare = LabelRarityClassifier.Classify(242, 241, 121);

    Assert.Equal(LabelRarity.Unique, unique);
    Assert.Equal(LabelRarity.Other, rare);
  }
}
