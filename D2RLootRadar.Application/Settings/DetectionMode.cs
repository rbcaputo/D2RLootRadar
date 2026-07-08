namespace D2RLootRadar.Application.Settings;

/// <summary>
/// Which detections the monitoring pipeline keeps after OCR, before they're matched against the watch list.
/// 
/// <para>
/// A single selection rather than independent toggles, because the three cases are mutually exclusive in-game:
/// an item can never be both Unique and Superior at once -
/// Superior is a quality modifier that only ever appears on white or gray-label items
/// (Normal, Exceptional, or Elite base items that rolled the modifier),
/// never on Magic, Rare, Set, or Unique items.
/// Modeling this as one enum instead of two bools makes that mutual exclusivity structural rather than a
/// UI convention someone could violate.
/// </para>
/// </summary>
public enum DetectionMode
{
  /// <summary>
  /// No filtering - every detection that matches the eatch list alerts.
  /// </summary>
  All,

  /// <summary>
  /// Only tan/gold (Unique) labels are matched against the watch list.
  /// </summary>
  UniqueOnly,

  /// <summary>
  /// Only labels whose recognized text starts with the "Superior" quality prefix are matched.
  /// </summary>
  SuperiorOnly
}
