using D2RLootRadar.Application.Contracts;

namespace D2RLootRadar.Infrastructure.FuzzyMatcher;

/// <summary>
/// Computes string similarity using a normalized Levenshtein distance augmented with
/// a partial (sliding-window) score.
/// 
/// <para>
/// <strong>Full score:</strong>
/// <c> 1 - editDistance / max(length_a, length_b)</c>
/// </para>
/// 
/// <para>
/// <strong>Partial score:</strong>
/// the best score achieved by sliding a window of <c>length(shorter)</c> characters across the longer string.
/// This handles OCR truncations - e.g. "Monarchi" still matches "Monarch" via an exact
/// substring window (1.0) instead of being penalized down to ~87% by the extra character.
/// </para>
/// 
/// The final result is <c>Max(fullScore, partialScore)</c>.
/// </summary>
public sealed class FuzzyMatcher : IFuzzyMatcher
{
  /// <inheritdoc />
  public double Similarity(string source, string target)
  {
    if (
      string.IsNullOrEmpty(source) ||
      string.IsNullOrEmpty(target)
    ) return 0.0;

    string a = Normalize(source);
    string b = Normalize(target);
    if (a == b)
      return 1.0;

    double full = FullSimilarity(a, b);
    double partial = PartialSimilarity(a, b);

    return Math.Max(full, partial);
  }

  // --- Private helpers -----

  /// <summary>
  /// Whole-string similarity: <c>1 - editDistance / max(length_a, length_b)</c>.
  /// </summary>
  private static double FullSimilarity(string a, string b)
  {
    int maxLength = Math.Max(a.Length, b.Length);

    return maxLength == 0
      ? 1.0
      : 1.0 - (double)LevenshteinDistance(a, b) / maxLength;
  }

  /// <summary>
  /// Slides a window the size of the shorter string across the longer one, scoring each position and keeping the best.
  /// Falls back to <see cref="FullSimilarity"/> when the strings are equal length,
  /// or when the shorter string is too small a fraction of the longer one to make a
  /// windowed comparison meaningful (see the 70% guard below).
  /// </summary>
  private static double PartialSimilarity(string a, string b)
  {
    // Slide the shorter string across the longer.
    string shorter = a.Length <= b.Length ? a : b;
    string longer = a.Length <= b.Length ? b : a;
    if (shorter.Length == longer.Length)
      return FullSimilarity(shorter, longer);

    // Require the shorter string to be at least 70% of the longer.
    // Prevents short common words ("rune", "axe") from partially matching compound
    // catalog names ("El Rune", "Hand Axe") via substring coincidence.
    if ((double)shorter.Length / longer.Length < 0.70)
      return FullSimilarity(shorter, longer);

    int window = shorter.Length;
    double best = 0.0;

    for (int i = 0; i <= longer.Length - window; i++)
    {
      string slice = longer.Substring(i, window);
      double score
        = 1.0 - (double)LevenshteinDistance(shorter, slice) / window;
      if (score > best)
        best = score;
    }

    return best;
  }

  /// <summary>
  /// Standard DP Levenshtein with a two-row rolling array (O(min(m,n)) space).
  /// </summary>
  private static int LevenshteinDistance(string a, string b)
  {
    if (a.Length == 0)
      return b.Length;
    if (b.Length == 0)
      return a.Length;

    // Ensure 'a' is the shorter string to keep allocations small.
    if (a.Length > b.Length)
      (a, b) = (b, a);

    int[] previous = new int[a.Length + 1];
    int[] current = new int[a.Length + 1];

    for (int i = 0; i <= a.Length; i++)
      previous[i] = i;

    for (int j = 1; j <= b.Length; j++)
    {
      current[0] = j;

      for (int i = 1; i <= a.Length; i++)
      {
        int cost = a[i - 1] == b[j - 1] ? 0 : 1;

        current[i] = Math.Min(
          Math.Min(previous[i] + 1, current[i - 1] + 1),
          previous[i - 1] + cost
        );
      }

      Array.Copy(current, previous, a.Length + 1);
    }

    return previous[a.Length];
  }

  /// <summary>
  /// Lowercases and trims for case/whitespace-insensitive comparison.
  /// </summary>
  private static string Normalize(string str)
    => str.ToLowerInvariant().Trim();
}
