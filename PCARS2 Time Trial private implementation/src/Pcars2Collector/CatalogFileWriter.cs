namespace Pcars2Collector;

public sealed class CatalogFileWriter(IHostEnvironment environment, ILogger<CatalogFileWriter> logger)
{
    private readonly HashSet<(string Name, string Qualifier)> vehicles = [];
    private readonly HashSet<(string Name, string Qualifier)> tracks = [];
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<(int VehicleCount, int TrackCount)> WriteAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var newVehicles = new List<(string Name, string Qualifier)>();
            var newTracks = new List<(string Name, string Qualifier)>();

            if (!string.IsNullOrWhiteSpace(snapshot.CarName))
            {
                var vehicle = (snapshot.CarName, snapshot.CarClassName);
                if (!vehicles.Contains(vehicle))
                {
                    newVehicles.Add(vehicle);
                }
            }

            if (!string.IsNullOrWhiteSpace(snapshot.TrackLocation))
            {
                var track = (snapshot.TrackLocation, snapshot.TrackVariation);
                if (!tracks.Contains(track))
                {
                    newTracks.Add(track);
                }
            }

            if (newVehicles.Count == 0 && newTracks.Count == 0)
            {
                return (vehicles.Count, tracks.Count);
            }

            var logDirectory = Path.Combine(environment.ContentRootPath, "logs");
            var logPath = Path.Combine(logDirectory, "vehicle-track-catalog.csv");
            var lines = new List<string>();
            lines.AddRange(newVehicles.Select(vehicle => CsvRow(DateTimeOffset.UtcNow, "vehicle", vehicle.Name, vehicle.Qualifier)));
            lines.AddRange(newTracks.Select(track => CsvRow(DateTimeOffset.UtcNow, "track", track.Name, track.Qualifier)));

            Directory.CreateDirectory(logDirectory);
            var includeHeader = !File.Exists(logPath) || new FileInfo(logPath).Length == 0;
            var content = (includeHeader ? "observed_at_utc,type,name,qualifier" + Environment.NewLine : "") +
                          string.Join(Environment.NewLine, lines) + Environment.NewLine;
            await File.AppendAllTextAsync(logPath, content, cancellationToken);

            vehicles.UnionWith(newVehicles);
            tracks.UnionWith(newTracks);
            foreach (var vehicle in newVehicles)
            {
                logger.LogInformation("Catalog captured vehicle {Vehicle} ({Class})", vehicle.Name, vehicle.Qualifier);
            }
            foreach (var track in newTracks)
            {
                logger.LogInformation("Catalog captured track {Track} ({Variation})", track.Name, track.Qualifier);
            }

            return (vehicles.Count, tracks.Count);
        }
        catch (IOException exception)
        {
            logger.LogError(exception, "Could not write vehicle and track catalog");
            return (vehicles.Count, tracks.Count);
        }
        finally
        {
            gate.Release();
        }
    }

    private static string CsvRow(DateTimeOffset observedAt, string type, string name, string qualifier) =>
        string.Join(',', observedAt.ToString("O"), Escape(type), Escape(name), Escape(qualifier));

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}