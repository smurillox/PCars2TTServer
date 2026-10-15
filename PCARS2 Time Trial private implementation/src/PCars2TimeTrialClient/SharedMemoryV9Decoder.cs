using System.Buffers.Binary;
using System.Text;
using Pcars2Collector;

namespace PCars2TimeTrialClient;

public static class SharedMemoryV9Decoder
{
    public static TelemetrySnapshot? Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < SharedMemoryV9Layout.SnapshotLength ||
            ReadUInt32(data, 0) != SharedMemoryV9Layout.Version)
        {
            return null;
        }

        var participantIndex = ReadInt32(data, SharedMemoryV9Layout.ViewedParticipantIndexOffset);
        var participantCount = ReadInt32(data, SharedMemoryV9Layout.ParticipantCountOffset);
        var participantIsAvailable = participantIndex >= 0 &&
                                     participantIndex < participantCount &&
                                     participantIndex < SharedMemoryV9Layout.MaxParticipants;
        var participantOffset = SharedMemoryV9Layout.ParticipantsOffset +
                                Math.Clamp(participantIndex, 0, SharedMemoryV9Layout.MaxParticipants - 1) *
                                SharedMemoryV9Layout.ParticipantSize;
        var lastLapOffset = SharedMemoryV9Layout.ParticipantLastLapTimesOffset + participantIndex * sizeof(float);
        var invalidatedOffset = SharedMemoryV9Layout.ParticipantLapInvalidatedOffset + participantIndex;

        var gameState = ReadUInt32(data, 8);
        var sessionState = ReadUInt32(data, 12);
        var raceState = ReadUInt32(data, 16);

        return new TelemetrySnapshot(
            (int)ReadUInt32(data, 4),
            participantIsAvailable
                ? ReadString(data, participantOffset + SharedMemoryV9Layout.ParticipantNameOffset, 64)
                : "unknown",
            gameState.ToString(),
            sessionState.ToString(),
            raceState.ToString(),
            ReadString(data, SharedMemoryV9Layout.CarNameOffset, 64),
            ReadString(data, SharedMemoryV9Layout.CarClassNameOffset, 64),
            ReadString(data, SharedMemoryV9Layout.TrackLocationOffset, 64),
            ReadString(data, SharedMemoryV9Layout.TrackVariationOffset, 64),
            data[SharedMemoryV9Layout.LapInvalidatedOffset] != 0,
            participantIsAvailable && data[invalidatedOffset] != 0,
            participantIsAvailable
                ? ReadUInt32(data, participantOffset + SharedMemoryV9Layout.ParticipantLapsCompletedOffset)
                : 0,
            ReadSingle(data, SharedMemoryV9Layout.BestLapTimeOffset),
            participantIsAvailable ? ReadSingle(data, lastLapOffset) : 0,
            ReadSingle(data, SharedMemoryV9Layout.CurrentTimeOffset),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    public static uint ReadSequenceNumber(ReadOnlySpan<byte> data) =>
        ReadUInt32(data, SharedMemoryV9Layout.SequenceNumberOffset);

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, sizeof(uint)));

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, sizeof(int)));

    private static float ReadSingle(ReadOnlySpan<byte> data, int offset) =>
        BitConverter.Int32BitsToSingle(ReadInt32(data, offset));

    private static string ReadString(ReadOnlySpan<byte> data, int offset, int length)
    {
        var bytes = data.Slice(offset, length);
        var nullTerminator = bytes.IndexOf((byte)0);
        if (nullTerminator >= 0)
        {
            bytes = bytes[..nullTerminator];
        }

        return Encoding.UTF8.GetString(bytes).Trim();
    }
}
