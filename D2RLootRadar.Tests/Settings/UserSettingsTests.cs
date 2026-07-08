using D2RLootRadar.Application.Settings;

namespace D2RLootRadar.Tests.Settings;

public class UserSettingsTests
{
  [Fact]
  public void Defaults_MatchDocumentedValues()
  {
    UserSettings settings = new();

    Assert.Equal(0.80, settings.FuzzyMatchThreshold);
    Assert.Equal(800, settings.BeepFrequencyHz);
    Assert.Equal(200, settings.BeepDurationMs);
    Assert.Equal(10, settings.BeepVolume);
    Assert.Equal(2, settings.MarkerDisplaySeconds);
    Assert.Equal(DetectionMode.All, settings.Mode);
    Assert.True(settings.OverlayEnabled);
    Assert.Empty(settings.SelectedItemBases);
  }

  [Theory]
  [InlineData(-0.5, 0.0)]
  [InlineData(1.5, 1.0)]
  [InlineData(0.5, 0.5)]
  public void FuzzyMatchThreshold_ClampsToZeroOneRange(double input, double expected)
  {
    UserSettings settings = new()
    {
      FuzzyMatchThreshold = input
    };

    Assert.Equal(expected, settings.FuzzyMatchThreshold);
  }

  [Theory]
  [InlineData(50, 100)]
  [InlineData(6_000, 5_000)]
  [InlineData(1_200, 1_200)]
  public void BeepFrerquencyHz_ClampsTo100_5000Range(int input, int expected)
  {
    UserSettings settings = new()
    {
      BeepFrequencyHz = input
    };

    Assert.Equal(expected, settings.BeepFrequencyHz);
  }

  [Theory]
  [InlineData(0, 1)]
  [InlineData(10_000, 5_000)]
  public void BeepDurationMs_ClampsTo1_5000Range(int input, int expected)
  {
    UserSettings settings = new()
    {
      BeepDurationMs = input
    };

    Assert.Equal(expected, settings.BeepDurationMs);
  }

  [Theory]
  [InlineData(-10, 0)]
  [InlineData(150, 100)]
  public void BeepVolume_ClampsTo0_100Range(int input, int expected)
  {
    UserSettings settings = new()
    {
      BeepVolume = input
    };

    Assert.Equal(expected, settings.BeepVolume);
  }

  [Theory]
  [InlineData(0, 1)]
  [InlineData(15, 10)]
  [InlineData(5, 5)]
  public void MarkerDisplaySeconds_ClampsTo1_50Range(int input, int expected)
  {
    UserSettings settings = new()
    {
      MarkerDisplaySeconds = input
    };

    Assert.Equal(expected, settings.MarkerDisplaySeconds);
  }
}
