namespace D2RLootRadar.Application.Contracts;

/// <summary>
/// A single matched item's absolute screen position, used by the overlay to place a marker.
/// ItemName is carried for logging/diagnostics only - the overlay itself renders no text.
/// </summary>
public readonly record struct DetectionMarker(string ItemName, int ScreenX, int ScreenY);
