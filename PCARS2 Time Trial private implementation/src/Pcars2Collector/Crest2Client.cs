using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pcars2Collector;

public interface ITelemetrySource
{
    Task<TelemetrySnapshot?> ReadAsync(CancellationToken cancellationToken);
}

public sealed class Crest2Client(HttpClient httpClient, ILogger<Crest2Client> logger) : ITelemetrySource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public async Task<TelemetrySnapshot?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetAsync(
                "crest2/v1/api?buildInfo=true&gameStates=true&participants=true&vehicleInformation=true&eventInformation=true&timings=true",
                cancellationToken);

            if (response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.Conflict)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<Crest2Payload>(JsonOptions, cancellationToken);
            return payload?.ToSnapshot();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("PCARS2 telemetry bridge did not respond before the request timeout");
            return null;
        }
        catch (HttpRequestException exception)
        {
            logger.LogDebug(exception, "PCARS2 telemetry bridge is unavailable");
            return null;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "PCARS2 telemetry bridge returned invalid JSON");
            return null;
        }
    }

    private sealed class Crest2Payload
    {
        public BuildInfo? Buildinfo { get; init; }
        public GameStates? GameStates { get; init; }
        public Participants? Participants { get; init; }
        public VehicleInformation? VehicleInformation { get; init; }
        public EventInformation? EventInformation { get; init; }
        public Timings? Timings { get; init; }
        public long Timestamp { get; init; }

        public TelemetrySnapshot? ToSnapshot()
        {
            if (Buildinfo is null || GameStates is null || VehicleInformation is null ||
                EventInformation is null || Timings is null)
            {
                return null;
            }

            return new TelemetrySnapshot(
                Buildinfo.Version,
                Participants?.PlayerName ?? "unknown",
                GameStates.GameState.ToString(),
                GameStates.SessionState.ToString(),
                GameStates.RaceState.ToString(),
                VehicleInformation.CarName,
                VehicleInformation.CarClassName,
                EventInformation.TrackLocation,
                EventInformation.TrackVariation,
                Timings.LapInvalidated,
                Timings.BestLapTime,
                Timings.LastLapTime,
                Timings.CurrentTime,
                Timestamp);
        }
    }

    private sealed class BuildInfo
    {
        [JsonPropertyName("mVersion")]
        public int Version { get; init; }
    }
    private sealed class GameStates
    {
        [JsonPropertyName("mGameState")]
        public int GameState { get; init; }
        [JsonPropertyName("mSessionState")]
        public int SessionState { get; init; }
        [JsonPropertyName("mRaceState")]
        public int RaceState { get; init; }
    }
    private sealed class VehicleInformation
    {
        [JsonPropertyName("mCarName")]
        public string CarName { get; init; } = "";
        [JsonPropertyName("mCarClassName")]
        public string CarClassName { get; init; } = "";
    }
    private sealed class Participants
    {
        [JsonPropertyName("mViewedParticipantIndex")]
        public int ViewedParticipantIndex { get; init; }
        [JsonPropertyName("mParticipantInfo")]
        public List<ParticipantInfo> ParticipantInfo { get; init; } = [];

        public string PlayerName => ViewedParticipantIndex >= 0 && ViewedParticipantIndex < ParticipantInfo.Count
            ? ParticipantInfo[ViewedParticipantIndex].Name
            : "unknown";
    }
    private sealed class ParticipantInfo
    {
        [JsonPropertyName("mName")]
        public string Name { get; init; } = "";
    }
    private sealed class EventInformation
    {
        [JsonPropertyName("mTrackLocation")]
        public string TrackLocation { get; init; } = "";
        [JsonPropertyName("mTrackVariation")]
        public string TrackVariation { get; init; } = "";
    }
    private sealed class Timings
    {
        [JsonPropertyName("mLapInvalidated")]
        public bool LapInvalidated { get; init; }
        [JsonPropertyName("mBestLapTime")]
        public double BestLapTime { get; init; }
        [JsonPropertyName("mLastLapTime")]
        public double LastLapTime { get; init; }
        [JsonPropertyName("mCurrentTime")]
        public double CurrentTime { get; init; }
    }
}
