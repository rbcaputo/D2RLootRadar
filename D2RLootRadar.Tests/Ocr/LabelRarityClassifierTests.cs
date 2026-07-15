using D2RLootRadar.Domain.Loot;
using D2RLootRadar.Infrastructure.Ocr;

namespace D2RLootRadar.Tests.Ocr;

public class LabelRarityClassifierTests
{
  // RGB values below are the measured average glyph color from real in-game label captures for
  // each of the six tiers - not synthetic "ideal" colors,
  // so these tests double as a regression guard on the calibration itself.
  //
  // RuneMaterial and Shard are measured the same way, from Rune and Worldstone Shard captures respectively -
  // averaged over every glyph pixel with saturation > 0.25 and value > 0.45.

  [Theory]
  [InlineData(230, 230, 230, LabelRarity.Normal)] // white
  [InlineData(157, 157, 153, LabelRarity.EtherealSocketed)] // gray
  [InlineData(140, 140, 232, LabelRarity.Magic)] // blue
  [InlineData(242, 241, 121, LabelRarity.Rare)] // yellow
  [InlineData(49, 222, 47, LabelRarity.Set)] // green
  [InlineData(199, 187, 149, LabelRarity.Unique)] // tan/gold
  [InlineData(215, 159, 5, LabelRarity.RuneMaterial)] // orange
  [InlineData(218, 92, 92, LabelRarity.Shard)] // red
  public void Classify_NonUniqueTier_ReturnsOthers(
    byte r,
    byte g,
    byte b,
    LabelRarity expected
  )
  {
    LabelRarity result = LabelRarityClassifier.Classify(r, g, b);

    Assert.Equal(expected, result);
  }

  [Fact]
  public void Classify_UnrecognizedColor_ReturnsUnknown()
  {
    // Magenta - saturated and colorful, but not a hue D2R renders labels in.
    // Note:
    // a low-saturation dark color (e.g. pure black) is NOT an "unrecognized" case -
    // the achromatic branch legitimately calssifies any dark, colorless pixels as EtherealSocketed.
    // There's no "too dark to be anything" cutoff in the classifier;
    // Unknown is reserved for a genuinely out-of-band hue, not for darkness.
    LabelRarity result = LabelRarityClassifier.Classify(255, 0, 255);

    Assert.Equal(LabelRarity.Unknown, result);
  }

  [Fact]
  public void Classify_UniqueAndRare_DoNotCollideDespiteSharedHueFamily()
  {
    // The one genuinely close pair - both live in the ~45-65 degree hue band.
    // This test exists specifically to guard the saturation-based split between them.
    LabelRarity unique = LabelRarityClassifier.Classify(199, 187, 149);
    LabelRarity rare = LabelRarityClassifier.Classify(242, 241, 121);

    Assert.Equal(LabelRarity.Unique, unique);
    Assert.Equal(LabelRarity.Rare, rare);
    Assert.NotEqual(unique, rare);
  }

  [Fact]
  public void Classify_RuneMaterialAndRare_DoNotCollideDespiteSharedHueFamily()
  {
    // Rune/Material orange sits in the same huw band as Rare/Unique too,
    // just at much hihger saturation (~98% vs Rare's ~50%) - guard that split as wall.
    LabelRarity runeMaterial = LabelRarityClassifier.Classify(215, 159, 5);
    LabelRarity rare = LabelRarityClassifier.Classify(242, 241, 121);

    Assert.Equal(LabelRarity.RuneMaterial, runeMaterial);
    Assert.Equal(LabelRarity.Rare, rare);
    Assert.NotEqual(runeMaterial, rare);
  }

  [Fact]
  public void Classify_NormalAndEtherealSocketed_SplitOnBrightnessAlone()
  {
    // Both are achromatic; only Value should separate them.
    LabelRarity white = LabelRarityClassifier.Classify(230, 230, 230);
    LabelRarity gray = LabelRarityClassifier.Classify(157, 157, 153);

    Assert.Equal(LabelRarity.Normal, white);
    Assert.Equal(LabelRarity.EtherealSocketed, gray);
  }
}
