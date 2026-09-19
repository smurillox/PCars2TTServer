namespace Pcars2TTServer;

public sealed record LapQuery(
    string? Track,
    string? Vehicle,
    string? VehicleClass,
    string? Gamertag,
    int Limit = 500);

public sealed record LapRecord(
    long Id,
    string Gamertag,
    string Vehicle,
    string VehicleClass,
    string Track,
    long LapTimeMilliseconds,
    DateTimeOffset? LapDate,
    string SessionMode,
    string Game);

public sealed record LapFilterOptions(
    IReadOnlyList<string> Tracks,
    IReadOnlyList<string> Vehicles,
    IReadOnlyList<string> VehicleClasses,
    IReadOnlyList<string> Gamertags);
