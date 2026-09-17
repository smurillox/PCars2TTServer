namespace Pcars2Collector;

public sealed class LapDetector
{
    private string? carName;
    private string? trackKey;
    private double lastObservedLapTime;

    public LapCompleted? Observe(TelemetrySnapshot snapshot)
    {
        var currentTrackKey = $"{Normalize(snapshot.TrackLocation)}|{Normalize(snapshot.TrackVariation)}";
        var identityChanged = !string.Equals(carName, snapshot.CarName, StringComparison.Ordinal) ||
                              !string.Equals(trackKey, currentTrackKey, StringComparison.Ordinal);

        if (identityChanged)
        {
            carName = snapshot.CarName;
            trackKey = currentTrackKey;
            lastObservedLapTime = 0;
        }

        var completedLap = snapshot.LastLapTimeSeconds > 0 &&
                           snapshot.LastLapTimeSeconds != lastObservedLapTime;
        lastObservedLapTime = snapshot.LastLapTimeSeconds;

        if (!completedLap)
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
            !snapshot.LapInvalidated,
            DateTimeOffset.UtcNow,
            snapshot.SourceTimestamp);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
