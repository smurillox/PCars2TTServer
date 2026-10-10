namespace Pcars2Collector;

public sealed class CatalogCollectorWorker(
    ITelemetrySource telemetrySource,
    CatalogFileWriter catalogFileWriter,
    CollectorStatus collectorStatus,
    ILogger<CatalogCollectorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Vehicle and track catalog collection started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var snapshot = await telemetrySource.ReadAsync(stoppingToken);
            if (snapshot is not null)
            {
                var counts = await catalogFileWriter.WriteAsync(snapshot, stoppingToken);
                collectorStatus.RecordCatalogSnapshot(snapshot, counts.VehicleCount, counts.TrackCount);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
        }
    }
}