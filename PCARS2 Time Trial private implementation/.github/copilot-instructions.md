# Project Instructions

- Target .NET 8 and Windows for the collector.
- Keep PCARS2 telemetry access behind `ITelemetrySource`.
- Use milliseconds for persisted lap times.
- Ignore invalidated laps at collection time; only valid laps are persisted and forwarded.
- Keep backend transport separate from telemetry reading and lap detection.
