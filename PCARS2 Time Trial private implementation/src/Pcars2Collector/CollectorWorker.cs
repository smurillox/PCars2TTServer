namespace Pcars2Collector;

public sealed class CollectorWorker(
    ITelemetrySource telemetrySource,
    LapDetector lapDetector,
    LapEventFileWriter lapEventFileWriter,
    ILapEventSink lapEventSink,
    ILogger<CollectorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var snapshot = await telemetrySource.ReadAsync(stoppingToken);
            if (snapshot is not null)
            {
                var lap = lapDetector.Observe(snapshot);
                if (lap is not null && lap.Valid)
                {
                    await lapEventFileWriter.WriteAsync(lap, stoppingToken);
                    await lapEventSink.SendAsync(lap, stoppingToken);
                    logger.LogInformation(
                        "Lap completed: {Car} at {Track} in {LapTime} ms; valid={Valid}",
                        lap.CarName,
                        $"{lap.TrackLocation} / {lap.TrackVariation}",
                        lap.LapTimeMilliseconds,
                        lap.Valid);
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
        }
    }
}
