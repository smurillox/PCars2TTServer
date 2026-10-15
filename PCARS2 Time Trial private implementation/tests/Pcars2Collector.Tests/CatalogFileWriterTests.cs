using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Pcars2Collector.Tests;

public sealed class CatalogFileWriterTests
{
    [Fact]
    public async Task WritesOnlyNewVehicleAndTrackPairs()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);

        try
        {
            var writer = new CatalogFileWriter(new TestHostEnvironment(contentRoot), NullLogger<CatalogFileWriter>.Instance);
            var snapshot = Snapshot("BMW1SERIESMCOUPE", "Brands Hatch", "GP");

            var initialCounts = await writer.WriteAsync(snapshot, CancellationToken.None);
            await writer.WriteAsync(snapshot, CancellationToken.None);
            var updatedCounts = await writer.WriteAsync(snapshot with { TrackVariation = "Indy" }, CancellationToken.None);

            var rows = await File.ReadAllLinesAsync(Path.Combine(contentRoot, "logs", "vehicle-track-catalog.csv"));
            Assert.Equal((1, 1), initialCounts);
            Assert.Equal((1, 2), updatedCounts);
            Assert.Equal(4, rows.Length);
            Assert.Contains("\"BMW1SERIESMCOUPE\"", rows[1]);
            Assert.Contains("\"track\",\"Brands Hatch\",\"Indy\"", rows[3]);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    private static TelemetrySnapshot Snapshot(string carName, string trackLocation, string trackVariation) => new(
        9,
        "Test Driver",
        "GAME_INGAME_PLAYING",
        "SESSION_TIME_ATTACK",
        "RACESTATE_RACING",
        carName,
        "GT3",
        trackLocation,
        trackVariation,
        false,
        false,
        0,
        0,
        0,
        0,
        1);

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = nameof(CatalogFileWriterTests);
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}