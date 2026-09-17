namespace Pcars2Collector;

public sealed class LapEventFileWriter(IHostEnvironment environment, ILogger<LapEventFileWriter> logger)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task WriteAsync(LapCompleted lap, CancellationToken cancellationToken)
    {
        var logDirectory = Path.Combine(environment.ContentRootPath, "logs");
        Directory.CreateDirectory(logDirectory);

        var logPath = Path.Combine(logDirectory, "laps.log");
        var line = $"{lap.CapturedAt:O} | {(lap.Valid ? "VALID" : "INVALID")} | " +
                   $"{lap.CarName} | {lap.TrackLocation} / {lap.TrackVariation} | " +
                   $"{lap.LapTimeMilliseconds} ms{Environment.NewLine}";

        await gate.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(logPath, line, cancellationToken);
        }
        catch (IOException exception)
        {
            logger.LogError(exception, "Could not write lap event to {LapLogPath}", logPath);
        }
        finally
        {
            gate.Release();
        }
    }
}