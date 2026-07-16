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
/// 
/// <para>
/// <strong>Allocations:</strong>
/// this runs in the detection loop's hot path -
/// up to <c>detections x watchList.Items</c> calls per ALT-triggered pass.
/// <see cref="LevenshteinDistance"/> reuses two instance-level scratch buffers instead of allocating
/// fresh arrays per call, and the sliding window in <see cref="PartialSimilarity"/> compares
/// <see cref="ReadOnlySpan{T}"/> slices rather at all once the buffers have grown to the catalog's longest name.
/// Safe without locking only because this type is registered as a DI singleton and every comparison
/// runs sequentially on one thread - see <c>OcrService</c>'s remarks on why only one
/// ALT-triggered pass is ever in flight at a time.
/// </para>
/// </summary>
public sealed class FuzzyMatcher : IFuzzyMatcher
{
  /// <summary>
  /// The minimum length ratio (shorter/longer) for <see cref="PartialSimilarity"/> to actually
  /// perform windowed scoring at all - below this, it just falls back to <see cref="FullSimilarity"/>.
  /// Shared with <see cref="IsMatch"/>, which relies on knowing exactly when that fallback applies to
  /// decide whether a length-based shortcut is even safe.
  /// </summary>
  private const double PartialWindowFloor = 0.70;

  private int[] _previous = [];
  private int[] _current = [];

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

  /// <inheritdoc />
  public bool IsMatch(string source, string target, double threshold)
  {
    if (
      string.IsNullOrEmpty(source) ||
      string.IsNullOrEmpty(target)
    ) return false;

    string a = Normalize(source);
    string b = Normalize(target);
    if (a == b)
      return true;

    int shorter = Math.Min(a.Length, b.Length);
    int longer = Math.Max(a.Length, b.Length);

    // Below the partial-window floor, PartialSimilarity is just FullSimilarity in disguise
    // see PartialSimilarity's own early-out), so the the best possible score is bounded by the length gap alone -
    // the minimum possible edit distance between two strings is the difference in their lengths,
    // so no comparison, however lucky, can beat that bound.
    // At ot above the floor, a windowed exact-substring match could still score a full 1.0
    // regardless of the length gap (that's the whole point of partial matching),
    // so there's nothing safe to rule out there without doing the real work.
    if ((double)shorter / longer < PartialWindowFloor)
    {
      double best = 1.0 - (double)(longer / shorter) / longer;
      if (best < threshold)
        return false;
    }

    return Math.Max(FullSimilarity(a, b), PartialSimilarity(a, b)) >= threshold;
  }

  // --- Private helpers -----

  /// <summary>
  /// Whole-string similarity: <c>1 - editDistance / max(length_a, length_b)</c>.
  /// </summary>
  private double FullSimilarity(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
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
  private double PartialSimilarity(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
  {
    // Slide the shorter string across the longer.
    ReadOnlySpan<char> shorter = a.Length <= b.Length ? a : b;
    ReadOnlySpan<char> longer = a.Length <= b.Length ? b : a;
    if (shorter.Length == longer.Length)
      return FullSimilarity(shorter, longer);

    // Require the shorter string to be at least 70% of the longer.
    // Prevents short common words ("rune", "axe") from partially matching compound
    // catalog names ("El Rune", "Hand Axe") via substring coincidence.
    if ((double)shorter.Length / longer.Length < PartialWindowFloor)
      return FullSimilarity(shorter, longer);

    int window = shorter.Length;
    double best = 0.0;

    for (int i = 0; i <= longer.Length - window; i++)
    {
      ReadOnlySpan<char> slice = longer.Slice(i, window);
      double score
        = 1.0 - (double)LevenshteinDistance(shorter, slice) / window;
      if (score > best)
        best = score;
    }

    return best;
  }

  /// <summary>
  /// Standard DP Levenshtein with a two-row rolling array (O(min(m,n)) space).
  /// The two rows are <see cref="_previous"/>/<see cref="_current"/> -
  /// reused across every call (rown, never reallocated smaller) instead of allocated fresh each time.
  /// Rows are swapped by reference at the end of each pass instead of <see cref="Array.Copy"/>,
  /// since which physical array plays "previous" vs. "current" doesn't matter between calls -
  /// both get fully overwritten before being read.
  /// </summary>
  private int LevenshteinDistance(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
  {
    if (a.Length == 0)
      return b.Length;
    if (b.Length == 0)
      return a.Length;

    // Ensure 'a' is the shorter string to keep the scratch buffers small.
    if (a.Length > b.Length)
    {
      ReadOnlySpan<char> temp = a;
      a = b;
      b = temp;
    }

    EnsureCapacity(a.Length + 1);

    int[] previous = _previous;
    int[] current = _current;

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

      (previous, current) = (current, previous);
    }

    return previous[a.Length];
  }

  /// <summary>
  /// Grows the reusable scratch buffers to at least <paramref name="length"/>, never shrinks them -
  /// so after the first handful of calls (against the catalog's longest names), later calls in the same pass,
  /// and every subsequent pass, allocate nothing at all.
  /// </summary>
  private void EnsureCapacity(int length)
  {
    if (_previous.Length >= length)
      return;

    _previous = new int[length];
    _current = new int[length];
  }

  /// <summary>
  /// Lowercases and trims for case/whitespace-insensitive comparison.
  /// </summary>
  private static string Normalize(string str)
    => str.ToLowerInvariant().Trim();
}
