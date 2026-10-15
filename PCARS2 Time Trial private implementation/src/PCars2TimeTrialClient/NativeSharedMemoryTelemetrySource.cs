using System.IO.MemoryMappedFiles;
using Pcars2Collector;

namespace PCars2TimeTrialClient;

public sealed class NativeSharedMemoryTelemetrySource(
    CollectorStatus status,
    ILogger<NativeSharedMemoryTelemetrySource> logger) : ITelemetrySource
{
    public Task<TelemetrySnapshot?> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var memory = MemoryMappedFile.OpenExisting("$pcars2$", MemoryMappedFileRights.Read);
            using var view = memory.CreateViewAccessor(0, SharedMemoryV9Layout.SnapshotLength, MemoryMappedFileAccess.Read);
            var bytes = new byte[SharedMemoryV9Layout.SnapshotLength];

            for (var attempt = 0; attempt < 5; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sequenceBefore = view.ReadUInt32(SharedMemoryV9Layout.SequenceNumberOffset);
                if ((sequenceBefore & 1) != 0)
                {
                    continue;
                }

                if (view.ReadArray(0, bytes, 0, bytes.Length) != bytes.Length)
                {
                    status.SetCrest2Available(false);
                    return Task.FromResult<TelemetrySnapshot?>(null);
                }

                var copiedSequence = SharedMemoryV9Decoder.ReadSequenceNumber(bytes);
                var sequenceAfter = view.ReadUInt32(SharedMemoryV9Layout.SequenceNumberOffset);
                if (sequenceBefore != copiedSequence || sequenceBefore != sequenceAfter || (sequenceAfter & 1) != 0)
                {
                    continue;
                }

                if (BitConverter.ToUInt32(bytes, 0) != SharedMemoryV9Layout.Version)
                {
                    status.SetCrest2Available(false);
                    logger.LogWarning("PCARS2 shared memory version {Version} is unsupported; expected {ExpectedVersion}",
                        BitConverter.ToUInt32(bytes, 0), SharedMemoryV9Layout.Version);
                    return Task.FromResult<TelemetrySnapshot?>(null);
                }

                var snapshot = SharedMemoryV9Decoder.Decode(bytes);
                status.SetCrest2Available(snapshot is not null);
                return Task.FromResult(snapshot);
            }

            logger.LogDebug("PCARS2 shared memory changed during all snapshot attempts");
            status.SetCrest2Available(true);
            return Task.FromResult<TelemetrySnapshot?>(null);
        }
        catch (FileNotFoundException)
        {
            status.SetCrest2Available(false);
            logger.LogDebug("PCARS2 shared memory map $pcars2$ is not available");
            return Task.FromResult<TelemetrySnapshot?>(null);
        }
        catch (IOException exception)
        {
            status.SetCrest2Available(false);
            logger.LogDebug(exception, "Could not read PCARS2 shared memory");
            return Task.FromResult<TelemetrySnapshot?>(null);
        }
        catch (UnauthorizedAccessException exception)
        {
            status.SetCrest2Available(false);
            logger.LogWarning(exception, "Access to PCARS2 shared memory was denied");
            return Task.FromResult<TelemetrySnapshot?>(null);
        }
    }
}
