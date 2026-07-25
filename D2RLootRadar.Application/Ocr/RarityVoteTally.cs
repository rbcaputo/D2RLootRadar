using D2RLootRadar.Domain.Loot;

namespace D2RLootRadar.Application.Ocr;

/// <summary>
/// Resolves a per-pixel color-classification vote tally into one <see cref="LabelRarity"/> verdict
/// plus a confidence score.
/// 
/// <para>
/// Lives here rather than inline in <c>OcrService.SampleRarity</c> so the vote-resolution math -
/// where the actual under-counted-confidence bug lived - can be unit tested directly against a
/// plain vote array, without needing a reference to Infrastructure's GDI+/<c>Bitmap</c> machinery to
/// construct real pixel data first.
/// </para>
/// </summary>
public static class RarityVoteTally
{
  /// <summary>
  /// Number of members in <see cref="LabelRarity"/> -
  /// the fixed length <see cref="Resolve"/> expects its <c>votes</c> span to have,
  /// and what <c>OcrService.SampleRarity</c> sizes its stack-allocated vote buffer to.
  /// Single source of truth for that count, kept as a literal (rather than <c>Enum.GetValues</c>)
  /// so the caller's vote buffer can live on the stack.
  /// Unknown, Normal, EtherealSocketed, Magic, Rare, Set, Unique, RuneMaterial, Shard = 9.
  /// </summary>
  public const int TierCount = 9;

  /// <summary>
  /// Resolves a per-tier pixel vote tally into a single classified rarity and confidence score.
  /// 
  /// <para>
  /// Majority vote among every tier except <see cref="LabelRarity.Unknown"/> -
  /// a few out-of-band edge pixels shouldn't be able to veto an otherwise clear verdict, so Unknown never wins outright.
  /// If literally every pixel came back Unknown (no real color evidence at all,
  /// or <paramref name="votes"/> is entirely zero), the result is Unknown with confidence 0.
  /// </para>
  /// 
  /// <para>
  /// Confidence is the winning tier's share of pixels that voted for *some* color -
  /// it excludes Unknown-classified pixels from the denominator entirely.
  /// An Unknown pixel (anti-aliased edge noise, upscaling artifacts) didn't vote against the winning tier;
  /// it didn't vote at all, so it must not be alloed to dilute confidence in a verdict every
  /// classified pixel actually agreed on.
  /// </para>
  /// </summary>
  /// <param name="votes">
  /// One vote count per <see cref="LabelRarity"/> member, indexed by its underlying int value.
  /// Expected to have length <see cref="TierCount"/>.
  /// </param>
  public static (LabelRarity Classified, double Confidence) Resolve(ReadOnlySpan<int> votes)
  {
    LabelRarity classified = LabelRarity.Unknown;
    int bestVotes = -1;

    for (int tier = 0; tier < votes.Length; tier++)
    {
      if (tier == (int)LabelRarity.Unknown)
        continue;

      if (votes[tier] > bestVotes)
      {
        bestVotes = votes[tier];
        classified = (LabelRarity)tier;
      }
    }

    if (bestVotes <= 0)
      return (LabelRarity.Unknown, 0.0);

    int classifiedVotes = 0;

    foreach (int voteCount in votes)
      classifiedVotes += voteCount;

    classifiedVotes -= votes[(int)LabelRarity.Unknown];

    return (classified, (double)bestVotes / classifiedVotes);
  }
}
