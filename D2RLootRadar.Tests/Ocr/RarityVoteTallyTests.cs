using D2RLootRadar.Application.Ocr;
using D2RLootRadar.Domain.Loot;

namespace D2RLootRadar.Tests.Ocr;

public class RarityVoteTallyTests
{
  private static int[] Votes(params (LabelRarity Tier, int Count)[] entries)
  {
    int[] votes = new int[RarityVoteTally.TierCount];

    foreach ((LabelRarity tier, int count) in entries)
      votes[(int)tier] = count;

    return votes;
  }

  [Fact]
  public void Resolve_AllZeroVotes_ReturnsUnknownWithZeroConfidence()
  {
    (LabelRarity classified, double confidence) = RarityVoteTally.Resolve(new int[RarityVoteTally.TierCount]);

    Assert.Equal(LabelRarity.Unknown, classified);
    Assert.Equal(0.0, confidence);
  }

  [Fact]
  public void Resolve_EveryPixelUnknown_ReturnsUnknownWithZeroConfidence()
  {
    int[] votes = Votes((LabelRarity.Unknown, 10));

    (LabelRarity classified, double confidence) = RarityVoteTally.Resolve(votes);

    Assert.Equal(LabelRarity.Unknown, classified);
    Assert.Equal(0.0, confidence);
  }

  [Fact]
  public void Resolve_UnanimousVote_ReturnsThatTierWithFullConfidence()
  {
    int[] votes = Votes((LabelRarity.Normal, 42));

    (LabelRarity classified, double confidence) = RarityVoteTally.Resolve(votes);

    Assert.Equal(LabelRarity.Normal, classified);
    Assert.Equal(1.0, confidence);
  }

  [Fact]
  public void Resolve_UnknownVotesMixedIn_DoNotDiluteConfidence()
  {
    // The regression case: every pixel that got a real color classification agrred on Normal -
    // the Unknown-classified edge-noise pixels didn't vote against that,
    // so they must not lower confidence below the 60/60 = 1.0 every classified pixel actually agreed on.
    int[] votes = Votes((LabelRarity.Normal, 60), (LabelRarity.Unknown, 40));

    (LabelRarity classified, double confidence) = RarityVoteTally.Resolve(votes);

    Assert.Equal(LabelRarity.Normal, classified);
    Assert.Equal(1.0, confidence);
  }

  [Fact]
  public void Resolve_MinorityWrongTierAndUnknownBoth_ExcludesOnlyUnknownFromDenominator()
  {
    // 8 Normal, 1 Magic (a real, if minority, disagreement), 1 Unknown (didn't vote at all).
    // Confidence should be against the 9 pixels that actually voted for a color (8/9),
    // not against all 10 sampled pixels (8/10).
    int[] votes = Votes(
      (LabelRarity.Normal, 8),
      (LabelRarity.Magic, 1),
      (LabelRarity.Unknown, 1)
    );

    (LabelRarity classified, double confidence) = RarityVoteTally.Resolve(votes);

    Assert.Equal(LabelRarity.Normal, classified);
    Assert.Equal(8.0 / 9.0, confidence, precision: 10);
  }

  [Fact]
  public void Resolve_CloseSplitBetweenTwoRealTiers_ScoresProportionally()
  {
    int[] votes = Votes((LabelRarity.Magic, 6), (LabelRarity.Rare, 4));

    (LabelRarity classified, double confidence) = RarityVoteTally.Resolve(votes);

    Assert.Equal(LabelRarity.Magic, classified);
    Assert.Equal(0.6, confidence, precision: 10);
  }

  [Fact]
  public void Resolve_MinorityOutOfBandPixels_AreOutvotedNotVetoing()
  {
    // A handful of contaminated edge pixels landing on unrelated tiers shouldn't be able to flip an
    // otherwise clear majority verdict.
    int[] votes = Votes(
      (LabelRarity.Unique, 20),
      (LabelRarity.Rare, 1),
      (LabelRarity.RuneMaterial, 1)
    );

    (LabelRarity classified, double confidence) = RarityVoteTally.Resolve(votes);

    Assert.Equal(LabelRarity.Unique, classified);
    Assert.Equal(20.0 / 22.0, confidence, precision: 10);
  }

  [Fact]
  public void Resolve_ExactTieBetweenTwoTiers_PickLowerEnumValueDeterministically()
  {
    // Documents the actual tie-break behavior (first-seen-wins under strict '>') rather than
    // leaving it as an unspecified implementation detail.
    int[] votes = Votes((LabelRarity.Magic, 5), (LabelRarity.Rare, 5));

    (LabelRarity classified, _) = RarityVoteTally.Resolve(votes);

    Assert.Equal(LabelRarity.Magic, classified);
  }
}
