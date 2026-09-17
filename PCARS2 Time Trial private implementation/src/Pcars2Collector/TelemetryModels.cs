namespace Pcars2Collector;

public sealed record TelemetrySnapshot(
    int BuildVersion,
    string GameState,
    string SessionState,
    string RaceState,
    string CarName,
    string CarClassName,
    string TrackLocation,
    string TrackVariation,
    bool LapInvalidated,
    double BestLapTimeSeconds,
    double LastLapTimeSeconds,
    double CurrentTimeSeconds,
    long SourceTimestamp);

public sealed record LapCompleted(
    string CarName,
    string CarClassName,
    string TrackLocation,
    string TrackVariation,
    long LapTimeMilliseconds,
    bool Valid,
    DateTimeOffset CapturedAt,
    long SourceTimestamp);
