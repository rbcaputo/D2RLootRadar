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
}
