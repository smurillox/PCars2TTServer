namespace Pcars2Collector;

public sealed record LapDeliveryResult(
    LapDeliveryState State,
    int? StatusCode,
    string ResponseSummary)
{
    public bool BackendAvailable => State is not LapDeliveryState.Failed;
}
