namespace D2RLootRadar.Application.Contracts;

/// <summary>
/// Computes string similarity for matching OCR output against item base names.
/// </summary>
public interface IFuzzyMatcher
{
  /// <summary>
  /// Returns a normalized similarity score between 0.0 and 1.0.
  /// 1.0 = identical (case-insensitive). 0.0 = completely different.
  /// </summary>
  double Similarity(string source, string target);

  /// <summary>
  /// Whether source and target are similar enough to meet <paramref name="threshold"/>.
  /// 
  /// <para>
  /// Prefer this over <c>Similarity(source, target) >= threshold</c> in a hot loop -
  /// implementations may rule out a match from the string's lengths alone and skip the
  /// underlying computation entirely, without ever materializing the exact score.
  /// </para>
  /// </summary>
  bool IsMatch(string source, string target, double threshold);
}
