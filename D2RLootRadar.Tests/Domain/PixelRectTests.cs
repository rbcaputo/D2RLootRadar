using D2RLootRadar.Domain.Loot;

namespace D2RLootRadar.Tests.Domain;

public class PixelRectTests
{
  [Fact]
  public void CenterX_CenterY_ComputeMidpoint()
  {
    PixelRect rect = new(X: 10, Y: 20, Width: 100, Height: 50);

    Assert.Equal(60, rect.CenterX);
    Assert.Equal(45, rect.CenterY);
  }

  [Fact]
  public void CenterX_CenterY_ZeroSizeRect_EqualsOrigin()
  {
    PixelRect rect = new(X: 5, Y: 5, Width: 0, Height: 0);

    Assert.Equal(5, rect.CenterX);
    Assert.Equal(5, rect.CenterY);
  }
}
