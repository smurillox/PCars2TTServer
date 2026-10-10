namespace Pcars2Collector;

public enum LapDeliveryState
{
    Captured,
    Accepted,
    Rejected,
    Unmapped,
    Failed
}

public sealed record LapActivity(
    LapCompleted Lap,
    LapDeliveryState DeliveryState,
    int? ResponseStatusCode,
    string ResponseSummary,
    DateTimeOffset UpdatedAt);

public sealed record CollectorStatusSnapshot(
    bool Crest2Available,
    bool BackendAvailable,
    LapActivity? LatestLap,
    IReadOnlyList<LapActivity> RecentLaps,
    TelemetrySnapshot? LatestCatalogSnapshot,
    int CatalogVehicleCount,
    int CatalogTrackCount);

public sealed class CollectorStatus
{
    private readonly object gate = new();
    private readonly List<LapActivity> recentLaps = [];
    private bool crest2Available;
    private bool backendAvailable;
    private LapActivity? latestLap;
    private TelemetrySnapshot? latestCatalogSnapshot;
    private int catalogVehicleCount;
    private int catalogTrackCount;

    public event EventHandler? Changed;

    public void SetCrest2Available(bool available)
    {
        lock (gate)
        {
            crest2Available = available;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetBackendAvailable(bool available)
    {
        lock (gate)
        {
            backendAvailable = available;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void RecordLap(LapActivity activity)
    {
        lock (gate)
        {
            latestLap = activity;
            recentLaps.Insert(0, activity);
            if (recentLaps.Count > 20)
            {
                recentLaps.RemoveAt(recentLaps.Count - 1);
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void RecordCatalogSnapshot(TelemetrySnapshot snapshot, int vehicleCount, int trackCount)
    {
        lock (gate)
        {
            latestCatalogSnapshot = snapshot;
            catalogVehicleCount = vehicleCount;
            catalogTrackCount = trackCount;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public CollectorStatusSnapshot Snapshot()
    {
        lock (gate)
        {
            return new CollectorStatusSnapshot(
                crest2Available,
                backendAvailable,
                latestLap,
                recentLaps.ToArray(),
                latestCatalogSnapshot,
                catalogVehicleCount,
                catalogTrackCount);
        }
    }
}
