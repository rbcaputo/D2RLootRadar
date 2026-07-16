namespace D2RLootRadar.Tests.FuzzyMatcher;

public class FuzzyMatcherTests
{
  private readonly Infrastructure.FuzzyMatcher.FuzzyMatcher _matcher = new();

  [Fact]
  public void Similarity_IdenticalStrings_ReturnsOne()
  {
    double result = _matcher.Similarity("Monarch", "Monarch");

    Assert.Equal(1.0, result);
  }

  [Fact]
  public void Similarity_EmptySource_ReturnsZero()
  {
    double result = _matcher.Similarity("", "Monarch");

    Assert.Equal(0.0, result);
  }

  [Fact]
  public void Similarity_EmptyTarget_ReturnsZero()
  {
    double result = _matcher.Similarity("Monarch", "");

    Assert.Equal(0.0, result);
  }

  [Fact]
  public void Similarity_CompletelyDifferentStrings_IsLow()
  {
    double result = _matcher.Similarity("Monarch", "Ring");

    Assert.True(
      result < 0.3,
      $"Expected a low score for unrelated strings, got {result}"
    );
  }

  [Fact]
  public void Similarity_OcrTruncation_MatchesViaPartialWindow()
  {
    // "Monarchi" contains "Monarch" as an exact substring,
    // so the sliding-window partial match finds a perfect (1.0) window even though the
    // full-string edit distance alone would only score 0.875 (7/8).
    // This is the exact scenario the class' XML doc describes -
    // pinned here so a future refactor can't silently regress OCR-truncation tolerance without a test failing.
    double result = _matcher.Similarity("Monarchii", "Monarch");

    Assert.Equal(1.0, result);
  }

  [Fact]
  public void Similarity_ShortWordAgainstUnrelatedCompoundName_DoesNotFalseMatch()
  {
    // Guards the 70% length-ratio floor in PartialSimilarity:
    // "rune" is short enough relative to "El Rune" that without the floor,
    // a sliding window could find a coincidental substring match.
    // This must stay meaningfully below the default 0.80 match threshold used in production settings.
    double result = _matcher.Similarity("rune", "El Rune");

    Assert.True(
      result < 0.80,
      $"Expected 'rune' vs 'El Rune' to stay below the match threshold, got {result}"
    );
  }

  [Theory]
  [InlineData("Ber Rune", "Ber Rune", 1.0)]
  [InlineData("B3r Rune", "Ber Rune", 0.7)] // one OCR misread character
  public void Similarity_KnownCases_MeetsExpectedFloor(
    string source,
    string target,
    double expectedFloor
  )
  {
    double result = _matcher.Similarity(source, target);

    Assert.True(
      result >= expectedFloor,
      $"Expected similarity >= {expectedFloor} for '{source}' vs '{target}', got {result}"
    );
  }

  [Fact]
  public void IsMatch_IdenticalStrings_ReturnsTrue()
  {
    bool result = _matcher.IsMatch("Monarch", "Monarch", threshold: 0.99);

    Assert.True(result);
  }

  [Fact]
  public void IsMatch_OcrTruncation_MatchesViaPartialMode()
  {
    // Same scenario as Similarity_OcrTruncation_MatchesViaPartialWindow -
    // pinned separately for IsMatch since it takes a different code path (the length-ratio fast path)
    // that Similarity() never touches, and that path must not short-circuit this case to false.
    bool result = _matcher.IsMatch("Monarchii", "Monarch", threshold: 0.99);

    Assert.True(result);
  }

  [Fact]
  public void IsMatch_LengthsRuleOutThreshold_ReturnsFalseWithoutFullComputation()
  {
    // "a" vs "Colossus Blade":
    // length ratio is far below the 70% partial-window floor, so IsMatch should reject this from lengths alone.
    // Correctness here matters more than the "without full computation" part of the name suggests -
    // that part isn't directly observable from a unit test, but Similarity_CompletelyDifferentStrings_IsLow-style
    // cases like this are exactly the ones the length-based fast path is meant to shortcut.
    bool result = _matcher.IsMatch("a", "Colossus Blade", threshold: 0.5);

    Assert.False(result);
  }

  [Fact]
  public void IsMatch_AgreesWithSimilarAtThreshold()
  {
    // Cross-check against the known-case Theory above, through the other entry point -
    // gaurds against IsMatch's early-exit bound ever diverging from what Similarity() would say.
    double similarity = _matcher.Similarity("B3r Rune", "Ber Rune");

    Assert.True(_matcher.IsMatch("B3r Rune", "Ber Rune", similarity));
    Assert.False(_matcher.IsMatch("B3r Rune", "Ber Rune", similarity + 0.01));
  }
}
