using System.Runtime.CompilerServices;

// Allows the test project to unit-test internal-but-pure logic
// (e.g. LootMonitoringService.StripQualityPrefix) without making it public API or
// instantiating the full service with all its dependencies.
[assembly: InternalsVisibleTo("D2RLootRadar.Tests")]
