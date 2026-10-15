namespace Pcars2Collector;

public sealed class LapDetector
{
    private string? carName;
    private string? gamertag;
    private string? trackKey;
    private double lastObservedLapTime;
    private uint lastObservedLapsCompleted;
    private bool currentLapInvalidated;

    public LapCompleted? Observe(TelemetrySnapshot snapshot)
    {
        var currentTrackKey = $"{Normalize(snapshot.TrackLocation)}|{Normalize(snapshot.TrackVariation)}";
        var identityChanged = !string.Equals(gamertag, snapshot.Gamertag, StringComparison.Ordinal) ||
                              !string.Equals(carName, snapshot.CarName, StringComparison.Ordinal) ||
                              !string.Equals(trackKey, currentTrackKey, StringComparison.Ordinal);
        var invalidatedNow = snapshot.LapInvalidated || snapshot.PlayerLapInvalidated;

        if (identityChanged)
        {
            gamertag = snapshot.Gamertag;
            carName = snapshot.CarName;
            trackKey = currentTrackKey;
            SetBaseline(snapshot, invalidatedNow);
            return null;
        }

        if (snapshot.LapsCompleted < lastObservedLapsCompleted)
        {
            SetBaseline(snapshot, invalidatedNow);
            return null;
        }

        var completedLap = snapshot.LapsCompleted > lastObservedLapsCompleted ||
                           (snapshot.LastLapTimeSeconds > 0 && snapshot.LastLapTimeSeconds != lastObservedLapTime);
        var completedLapInvalidated = currentLapInvalidated || invalidatedNow;

        lastObservedLapTime = snapshot.LastLapTimeSeconds;
        lastObservedLapsCompleted = snapshot.LapsCompleted;

        if (!completedLap)
        {
            currentLapInvalidated |= invalidatedNow;
            return null;
        }

        currentLapInvalidated = invalidatedNow;
        if (snapshot.LastLapTimeSeconds <= 0)
        {
            return null;
        }

        return new LapCompleted(
            snapshot.Gamertag,
            snapshot.CarName,
            snapshot.CarClassName,
            snapshot.TrackLocation,
            snapshot.TrackVariation,
            checked((long)Math.Round(snapshot.LastLapTimeSeconds * 1000, MidpointRounding.AwayFromZero)),
            !completedLapInvalidated,
            DateTimeOffset.UtcNow,
            snapshot.SourceTimestamp);
    }

    private void SetBaseline(TelemetrySnapshot snapshot, bool invalidated)
    {
        lastObservedLapTime = snapshot.LastLapTimeSeconds;
        lastObservedLapsCompleted = snapshot.LapsCompleted;
        currentLapInvalidated = invalidated;
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
