using Xunit;

namespace Pcars2Collector.Tests;

public sealed class LapDetectorTests
{
    [Fact]
    public void EmitsWhenLapCounterAdvancesAndSuppressesDuplicatePolls()
    {
        var detector = new LapDetector();
        var first = detector.Observe(Snapshot(0, false, 0));
        var completed = detector.Observe(Snapshot(91.234, false, 1));
        var duplicate = detector.Observe(Snapshot(91.234, false, 1));

        Assert.Null(first);
        Assert.NotNull(completed);
        Assert.Equal(91234, completed!.LapTimeMilliseconds);
        Assert.Null(duplicate);
    }

    [Fact]
    public void PreservesInvalidationFlagOnCompletedLap()
    {
        var detector = new LapDetector();
        detector.Observe(Snapshot(0, false, 0));
        detector.Observe(Snapshot(0, true, 0));

        var completed = detector.Observe(Snapshot(92.5, false, 1));

        Assert.NotNull(completed);
        Assert.False(completed!.Valid);
    }

    [Fact]
    public void LatchesPlayerInvalidationAndRejectsBothInvalidatedLaps()
    {
        var detector = new LapDetector();
        detector.Observe(Snapshot(0, false, 0));
        detector.Observe(Snapshot(0, false, 0, playerInvalidated: true));

        var firstInvalidLap = detector.Observe(Snapshot(65.061, false, 1, playerInvalidated: true));
        var secondInvalidLap = detector.Observe(Snapshot(65.061, false, 1));
        var secondInvalidLapCompletion = detector.Observe(Snapshot(64.9, false, 2));

        Assert.NotNull(firstInvalidLap);
        Assert.False(firstInvalidLap!.Valid);
        Assert.Null(secondInvalidLap);
        Assert.NotNull(secondInvalidLapCompletion);
        Assert.False(secondInvalidLapCompletion!.Valid);
    }

    [Fact]
    public void LapCounterDetectsEqualConsecutiveLapTimes()
    {
        var detector = new LapDetector();
        detector.Observe(Snapshot(0, false, 0));

        var first = detector.Observe(Snapshot(90, false, 1));
        var second = detector.Observe(Snapshot(90, false, 2));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.LapTimeMilliseconds, second!.LapTimeMilliseconds);
    }

    [Fact]
    public void IgnoresLapCounterAdvanceWithoutPositiveLastLapTime()
    {
        var detector = new LapDetector();
        detector.Observe(Snapshot(0, false, 0));

        var noTime = detector.Observe(Snapshot(0, false, 1));
        var nextLap = detector.Observe(Snapshot(88.5, false, 2));

        Assert.Null(noTime);
        Assert.NotNull(nextLap);
        Assert.Equal(88500, nextLap!.LapTimeMilliseconds);
    }

    private static TelemetrySnapshot Snapshot(
        double lastLapTime,
        bool timingInvalidated,
        uint lapsCompleted,
        bool playerInvalidated = false) => new(
        9,
        "Test Driver",
        "GAME_INGAME_PLAYING",
        "SESSION_TIME_ATTACK",
        "RACESTATE_RACING",
        "GT3 Car",
        "GT3",
        "Azure Circuit",
        "Grand Prix",
        timingInvalidated,
        playerInvalidated,
        lapsCompleted,
        91.234,
        lastLapTime,
        120,
        1);
}
