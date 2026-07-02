namespace D2RLootRadar.Domain.Loot;

/// <summary>
/// A simple, dependency-free pixel rectangle.
/// Kept separate from System.Drawing.Rectangle so Domain has no GDI dependency.
/// </summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
  /// <summary>
  /// Horizontal center point, used to place overlay markers.
  /// </summary>
  public int CenterX
    => X + Width / 2;

  /// <summary>
  /// Vertical center point, used to place overlay markers.
  /// </summary>
  public int CenterY
    => Y + Height / 2;
}
