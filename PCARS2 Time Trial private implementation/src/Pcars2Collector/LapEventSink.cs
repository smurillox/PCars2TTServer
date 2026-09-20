using System.Net;
using System.Net.Http.Json;

namespace Pcars2Collector;

public interface ILapEventSink
{
    Task<LapDeliveryResult> SendAsync(LapCompleted lap, CancellationToken cancellationToken);
}

public sealed class HttpLapEventSink(HttpClient httpClient, CollectorStatus status, ILogger<HttpLapEventSink> logger) : ILapEventSink
{
    public async Task<LapDeliveryResult> SendAsync(LapCompleted lap, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("api/laps", new
            {
                Gamertag = lap.Gamertag,
                Vehicle = lap.CarName,
                VehicleClass = lap.CarClassName,
                Track = $"{lap.TrackLocation} / {lap.TrackVariation}",
                Game = "Project CARS 2",
                SessionMode = "Time Attack",
                lap.LapTimeMilliseconds,
                ValidLap = lap.Valid,
                lap.CapturedAt,
                lap.SourceTimestamp
            }, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                logger.LogWarning("Backend rejected lap as a conflict");
                status.SetBackendAvailable(true);
                return new LapDeliveryResult(LapDeliveryState.Rejected, (int)response.StatusCode, "Not a new personal best");
            }

            response.EnsureSuccessStatusCode();
            status.SetBackendAvailable(true);
            var responseSummary = response.StatusCode == HttpStatusCode.Created ? "New personal best accepted" : $"Accepted ({(int)response.StatusCode})";
            logger.LogInformation("Lap sent to backend: {StatusCode}", response.StatusCode);
            return new LapDeliveryResult(LapDeliveryState.Accepted, (int)response.StatusCode, responseSummary);
        }
        catch (HttpRequestException exception)
        {
            status.SetBackendAvailable(false);
            logger.LogWarning(exception, "Could not send lap to backend; local log remains available");
            return new LapDeliveryResult(LapDeliveryState.Failed, null, "Backend unavailable");
        }
    }
}
