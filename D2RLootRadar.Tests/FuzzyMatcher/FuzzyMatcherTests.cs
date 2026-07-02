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
    double result = _matcher.Similarity("Monarchi", "Monarch");

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
}
