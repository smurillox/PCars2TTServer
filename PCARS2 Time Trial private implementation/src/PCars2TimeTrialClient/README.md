# PCars2TimeTrialClient

A second Windows collector that reads Project CARS 2 telemetry directly from the named shared-memory map `$pcars2$`. It does not use CREST2 or its HTTP API. The existing `Pcars2Collector` remains unchanged as the CREST2-based alternative.

## Requirements

- Windows with the .NET 8 Desktop Runtime (or publish self-contained).
- Project CARS 2 running with Shared Memory set to `Project CARS2`.
- Fedora API reachable at the `Pcars2:BackendBaseUrl` in `appsettings.json`.

## Build and run

From the repository root:

```powershell
dotnet run --project .\src\PCars2TimeTrialClient\PCars2TimeTrialClient.csproj
```

Publish a standalone folder:

```powershell
dotnet publish .\src\PCars2TimeTrialClient\PCars2TimeTrialClient.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o .\publish\PCars2TimeTrialClient
```

Run `PCars2TimeTrialClient.exe` from the publish folder. Set `Pcars2:BackendBaseUrl` in the published `clientsettings.json` if the Fedora VM address changes.

The reader accepts only shared-memory version 9. It checks the game's even/odd sequence number before and after copying a snapshot and skips unstable reads. The copied layout is intentionally bounded through the participant invalidation array, the last field currently required by lap collection. A different version fails closed rather than decoding guessed offsets.
