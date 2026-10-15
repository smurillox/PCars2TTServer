# PCARS2 Time Trial

Windows-native collector, MySQL-backed API, and web GUI for Project CARS 2 lap telemetry.

## Services

- `Pcars2Collector`: Windows process that reads PCARS2 telemetry and sends valid new laps.
- `PCars2TimeTrialClient`: alternate Windows collector that reads `$pcars2$` shared memory directly, without CREST2.
- `Pcars2TTServer`: API and MySQL persistence service on port `8080`.
- `Pcars2TTWeb`: browser interface on port `8081` for filtering and browsing best laps.

The collector opens a Windows status window showing CREST2 and backend connectivity, the latest captured lap, its backend result, and recent activity. It also shows a Windows notification after each backend response.

To try the direct shared-memory collector, see [src/PCars2TimeTrialClient/README.md](src/PCars2TimeTrialClient/README.md). It is a separate executable; the existing CREST2 collector remains available for comparison.

## Current slice

The collector polls the local CREST2 bridge, detects a changed `mLastLapTime`, rejects no data at the source, and emits a normalized lap event in the log. The source boundary is isolated behind `ITelemetrySource` so the bridge can later be replaced with a direct `$pcars2$` memory-map reader.

Required PCARS2 setup:

1. Enable the game's Shared Memory setting as `Project CARS2`.
2. Run CREST2 on the same Windows machine as the game.
3. Confirm `http://127.0.0.1:8180/crest2/v1/api` returns JSON.

## Build and run

Install the .NET 8 SDK, then from this directory:

```powershell
dotnet build .\src\Pcars2Collector\Pcars2Collector.csproj
dotnet run --project .\src\Pcars2Collector\Pcars2Collector.csproj
```

The collector polls at 10 Hz, uses the viewed participant's `mLapsCompleted` as the finish boundary, and combines CREST2's global `mLapInvalidated` with the participant's `mLapsInvalidated`. Invalidation is latched across the lap boundary so a brief or delayed flag cannot let an invalid lap through; consecutive invalid laps remain invalid. Invalidated laps and nonpositive lap times are ignored at collection time. A lap is represented in milliseconds and includes the car, car class, track, variation, validity flag, and source timestamp.

Valid completed laps are also appended to `src/Pcars2Collector/logs/laps.log` and posted to the Fedora backend configured by `Pcars2:BackendBaseUrl`. Invalidated laps are not written or forwarded; the frequent telemetry polling logs remain in the console.

## Vehicle and track catalog capture

Run the collector in catalog-only mode while gathering the names reported by the game:

```powershell
dotnet run --project .\src\Pcars2Collector\Pcars2Collector.csproj -- --catalog
```

Select each vehicle and track in PCARS2 while the catalog window is open. It shows the current CREST2 names and counts, and appends each distinct vehicle/class or track/variation pair to `logs/vehicle-track-catalog.csv` under the collector's working directory. Repeated telemetry samples are deduplicated. Catalog mode does not detect laps or contact the backend. The CSV preserves the raw game names for building the vehicle and track tables and their aliases.

Tests are under `tests/Pcars2Collector.Tests` and cover lap-counter completion, global and participant invalidation latching, consecutive invalid laps, numeric CREST2 invalidation flags, and catalog deduplication.

## Next steps

- The Fedora backend now accepts valid laps and retains only new bests per gamertag, vehicle, track, and game.
- Add an HTTPS transport and local outbox if the collector will operate across untrusted networks.
- Replace CREST2 polling with a direct `$pcars2$` reader when the exact PCARS2 v9 struct is pinned and tested against a live game.
