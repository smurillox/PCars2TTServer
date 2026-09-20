using Pcars2TTServer;
using Xunit;

namespace Pcars2Collector.Tests;

public sealed class LapKeyTests
{
    [Fact]
    public void TreatsSlashAndDashTrackSeparatorsAsEquivalent()
    {
        Assert.Equal(
            LapKey.CanonicalTrack("Willow_Springs / International_Raceway"),
            LapKey.CanonicalTrack("Willow_Springs-International_Raceway"));
    }
}
