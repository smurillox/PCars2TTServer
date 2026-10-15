namespace PCars2TimeTrialClient;

public static class SharedMemoryV9Layout
{
    public const uint Version = 9;
    public const int MaxParticipants = 64;
    public const int ParticipantSize = 100;
    public const int ParticipantsOffset = 28;
    public const int SequenceNumberOffset = 7320;
    public const int ParticipantLastLapTimesOffset = 9200;
    public const int ParticipantLapInvalidatedOffset = 9456;
    public const int SnapshotLength = 9520;

    public const int ViewedParticipantIndexOffset = 20;
    public const int ParticipantCountOffset = 24;
    public const int CarNameOffset = 6444;
    public const int CarClassNameOffset = 6508;
    public const int TrackLocationOffset = 6576;
    public const int TrackVariationOffset = 6640;
    public const int LapInvalidatedOffset = 6712;
    public const int BestLapTimeOffset = 6716;
    public const int CurrentTimeOffset = 6724;

    public const int ParticipantNameOffset = 1;
    public const int ParticipantLapsCompletedOffset = 88;
}
