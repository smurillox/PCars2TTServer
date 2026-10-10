using Pcars2TTServer;
using Xunit;

namespace Pcars2Collector.Tests;

public sealed class LapKeyTests
{
    [Theory]
    [InlineData("BMW1SeriesMCoupe", "BMW 1 Series M Coupe")]
    [InlineData("McLarenP1", "McLaren P1")]
    [InlineData("MercedesAMGC63CoupeS", "Mercedes-AMG C 63 Coupe S")]
    public void NormalizesFormattingDifferencesForVehicleAliases(string gameName, string databaseName)
    {
        Assert.Equal(VehicleKey.Normalize(gameName), VehicleKey.Normalize(databaseName));
    }

    [Fact]
    public void TreatsSlashAndDashTrackSeparatorsAsEquivalent()
    {
        Assert.Equal(
            LapKey.CanonicalTrack("Willow_Springs / International_Raceway"),
            LapKey.CanonicalTrack("Willow_Springs-International_Raceway"));
    }

    [Theory]
    [InlineData("BMW1SeriesMCoupe", "BMW 1 Series M Coupe")]
    [InlineData("McLarenP1", "McLaren P1")]
    [InlineData("MercedesAMGC63CoupeS", "Mercedes-AMG C 63 Coupe S")]
    public void NormalizesVehicleNameFormatting(string gameName, string databaseName)
    {
        Assert.Equal(VehicleKey.Normalize(gameName), VehicleKey.Normalize(databaseName));
    }
}
