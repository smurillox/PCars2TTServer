using Xunit;

namespace Pcars2Collector.Tests;

public sealed class LapDetectorTests
{
    [Fact]
    public void EmitsOnlyWhenLastLapTimeChanges()
    {
        var detector = new LapDetector();
        var first = detector.Observe(Snapshot(0, false));
        var completed = detector.Observe(Snapshot(91.234, false));
        var duplicate = detector.Observe(Snapshot(91.234, false));

        Assert.Null(first);
        Assert.NotNull(completed);
        Assert.Equal(91234, completed!.LapTimeMilliseconds);
        Assert.Null(duplicate);
    }

    [Fact]
    public void PreservesInvalidationFlagOnCompletedLap()
    {
        var detector = new LapDetector();
        detector.Observe(Snapshot(0, false));

        var completed = detector.Observe(Snapshot(92.5, true));

        Assert.NotNull(completed);
        Assert.False(completed!.Valid);
    }

    private static TelemetrySnapshot Snapshot(double lastLapTime, bool invalidated) => new(
        9,
        "Test Driver",
        "GAME_INGAME_PLAYING",
        "SESSION_TIME_ATTACK",
        "RACESTATE_RACING",
        "GT3 Car",
        "GT3",
        "Azure Circuit",
        "Grand Prix",
        invalidated,
        91.234,
        lastLapTime,
        120,
        1);
}
