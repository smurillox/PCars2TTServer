namespace Pcars2TTServer;

public sealed record LapEventRequest(
    string Gamertag,
    string Vehicle,
    string VehicleClass,
    string Track,
    string Game,
    string SessionMode,
    bool ValidLap,
    long LapTimeMilliseconds,
    DateTimeOffset CapturedAt,
    long SourceTimestamp);

public sealed record LapEventResponse(
    long Id,
    string Gamertag,
    string TrackKey,
    long LapTimeMilliseconds,
    long? PreviousBestMilliseconds,
    bool IsNewBest,
    DateTimeOffset CapturedAt);

public static class LapKey
{
    public static string Normalize(string value) => value.Trim().ToUpperInvariant();

    public static string CanonicalTrack(string value) =>
        value.Trim().Replace(" / ", "-").Replace("/", "-");

    public static string Track(string location, string variation) =>
        CanonicalTrack($"{location}-{variation}");
}
