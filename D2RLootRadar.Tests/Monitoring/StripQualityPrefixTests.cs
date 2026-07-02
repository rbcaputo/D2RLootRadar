using D2RLootRadar.Application.Monitoring;

namespace D2RLootRadar.Tests.Monitoring;

public class StripQualityPrefixTests
{
  [Theory]
  [InlineData("Cracked Sword", "Sword")]
  [InlineData("Crude Quilted Armor", "Quilted Armor")]
  [InlineData("Damaged Buckler", "Buckler")]
  [InlineData("Low Quality Pike", "Pike")]
  [InlineData("Superior Phase Blade", "Phase Blade")]
  [InlineData("superior monarch", "monarch")] // case-insensitive prefix match
  public void StripQualityPrefix_KnownPrefix_IsRemoved(string input, string expected)
  {
    string result = LootMonitoringService.StripQualityPrefix(input);

    Assert.Equal(expected, result);
  }

  [Fact]
  public void StripQualityPrefix_NoPrefix_ReturnsUnchanged()
  {
    string result = LootMonitoringService.StripQualityPrefix("Monarch");

    Assert.Equal("Monarch", result);
  }

  [Fact]
  public void StripQualityPrefix_UnknownPrefix_ReturnsUnchanged()
  {
    // "Supreme" isn't a recognized quality prefix - must not be stripped.
    string result = LootMonitoringService.StripQualityPrefix("Supreme Monarch");

    Assert.Equal("Supreme Monarch", result);
  }

  [Fact]
  public void StripQualityPrefix_PrefixWithNoFollowingWord_ReturnsUnchanged()
  {
    // "Superior" with nothing after it doesn't match "prefix + space", by design -
    // this guards against a bare prefix (mangled OCR read) being stripped to an empty string.
    string result = LootMonitoringService.StripQualityPrefix("Superior");

    Assert.Equal("Superior", result);
  }
}
