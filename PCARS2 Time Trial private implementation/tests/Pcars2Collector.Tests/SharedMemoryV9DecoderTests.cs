using System.Buffers.Binary;
using System.Text;
using PCars2TimeTrialClient;
using Xunit;

namespace Pcars2Collector.Tests;

public sealed class SharedMemoryV9DecoderTests
{
    [Fact]
    public void UsesExpectedVersion9SequenceAndParticipantArrayOffsets()
    {
        Assert.Equal(7320, SharedMemoryV9Layout.SequenceNumberOffset);
        Assert.Equal(9456, SharedMemoryV9Layout.ParticipantLapInvalidatedOffset);
        Assert.Equal(9520, SharedMemoryV9Layout.SnapshotLength);
    }

    [Fact]
    public void DecodesTheViewedParticipantAndV9TelemetryFields()
    {
        var data = new byte[SharedMemoryV9Layout.SnapshotLength];
        WriteUInt32(data, 0, SharedMemoryV9Layout.Version);
        WriteUInt32(data, 4, 1108);
        WriteUInt32(data, 8, 3);
        WriteUInt32(data, 12, 1);
        WriteUInt32(data, 16, 2);
        WriteInt32(data, SharedMemoryV9Layout.ViewedParticipantIndexOffset, 0);
        WriteInt32(data, SharedMemoryV9Layout.ParticipantCountOffset, 1);
        WriteString(data, SharedMemoryV9Layout.ParticipantsOffset + SharedMemoryV9Layout.ParticipantNameOffset, "Rockstar");
    WriteUInt32(data, SharedMemoryV9Layout.ParticipantsOffset + SharedMemoryV9Layout.ParticipantLapsCompletedOffset, 2);
        WriteString(data, SharedMemoryV9Layout.CarNameOffset, "BAC Mono");
        WriteString(data, SharedMemoryV9Layout.CarClassNameOffset, "Road B");
        WriteString(data, SharedMemoryV9Layout.TrackLocationOffset, "Circuit_of_the_Americas");
        WriteString(data, SharedMemoryV9Layout.TrackVariationOffset, "Club_Circuit");
        data[SharedMemoryV9Layout.LapInvalidatedOffset] = 1;
        WriteSingle(data, SharedMemoryV9Layout.BestLapTimeOffset, -1);
        WriteSingle(data, SharedMemoryV9Layout.BestLapTimeOffset + sizeof(float), 65.061f);
        WriteSingle(data, SharedMemoryV9Layout.CurrentTimeOffset, 6.10101f);
        WriteUInt32(data, SharedMemoryV9Layout.SequenceNumberOffset, 22);
        WriteSingle(data, SharedMemoryV9Layout.ParticipantLastLapTimesOffset, 65.061f);
        data[SharedMemoryV9Layout.ParticipantLapInvalidatedOffset] = 1;

        var snapshot = SharedMemoryV9Decoder.Decode(data);

        Assert.NotNull(snapshot);
        Assert.Equal(1108, snapshot!.BuildVersion);
        Assert.Equal("Rockstar", snapshot.Gamertag);
        Assert.Equal("BAC Mono", snapshot.CarName);
        Assert.Equal("Road B", snapshot.CarClassName);
        Assert.Equal("Circuit_of_the_Americas", snapshot.TrackLocation);
        Assert.Equal("Club_Circuit", snapshot.TrackVariation);
        Assert.True(snapshot.LapInvalidated);
        Assert.True(snapshot.PlayerLapInvalidated);
        Assert.Equal((uint)2, snapshot.LapsCompleted);
        Assert.Equal(65.061, snapshot.LastLapTimeSeconds, 3);
    }

    [Fact]
    public void RejectsUnsupportedVersionAndShortSnapshot()
    {
        var bytes = new byte[SharedMemoryV9Layout.SnapshotLength];
        WriteUInt32(bytes, 0, 8);

        Assert.Null(SharedMemoryV9Decoder.Decode(bytes));
        Assert.Null(SharedMemoryV9Decoder.Decode(bytes.AsSpan(0, 100)));
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)), value);

    private static void WriteInt32(byte[] bytes, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)), value);

    private static void WriteSingle(byte[] bytes, int offset, float value) =>
        WriteInt32(bytes, offset, BitConverter.SingleToInt32Bits(value));

    private static void WriteString(byte[] bytes, int offset, string value)
    {
        var encoded = Encoding.UTF8.GetBytes(value);
        encoded.CopyTo(bytes, offset);
    }
}
