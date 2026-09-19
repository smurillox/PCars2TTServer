# PCARS2 Time Trial

Windows-native collector, MySQL-backed API, and web GUI for Project CARS 2 lap telemetry.

## Services

- `Pcars2Collector`: Windows process that reads PCARS2 telemetry and sends valid new laps.
- `Pcars2TTServer`: API and MySQL persistence service on port `8080`.
- `Pcars2TTWeb`: browser interface on port `8081` for filtering and browsing best laps.

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

The collector polls at 10 Hz and logs valid completed laps. Invalidated laps are ignored at collection time. A lap is represented in milliseconds and includes the car, car class, track, variation, validity flag, and source timestamp.

Valid completed laps are also appended to `src/Pcars2Collector/logs/laps.log` and posted to the Fedora backend configured by `Pcars2:BackendBaseUrl`. Invalidated laps are not written or forwarded; the frequent telemetry polling logs remain in the console.

Tests are under `tests/Pcars2Collector.Tests` and cover lap completion transitions and invalidated laps.

## Next steps

- The Fedora backend now accepts valid laps and retains only new bests per gamertag, vehicle, track, and game.
- Add an HTTPS transport and local outbox if the collector will operate across untrusted networks.
- Replace CREST2 polling with a direct `$pcars2$` reader when the exact PCARS2 v9 struct is pinned and tested against a live game.
