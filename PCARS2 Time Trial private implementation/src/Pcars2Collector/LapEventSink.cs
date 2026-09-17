using System.Net;
using System.Net.Http.Json;

namespace Pcars2Collector;

public interface ILapEventSink
{
    Task SendAsync(LapCompleted lap, CancellationToken cancellationToken);
}

public sealed class HttpLapEventSink(HttpClient httpClient, ILogger<HttpLapEventSink> logger) : ILapEventSink
{
    public async Task SendAsync(LapCompleted lap, CancellationToken cancellationToken)
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
                return;
            }

            response.EnsureSuccessStatusCode();
            logger.LogInformation("Lap sent to backend: {StatusCode}", response.StatusCode);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not send lap to backend; local log remains available");
        }
    }
}
