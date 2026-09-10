using ERBossTrackerJP.Save.Exceptions;

namespace ERBossTrackerJP.Save.EventFlags;

public sealed class EventFlagReader
{
    public const uint FlagDivisor = 1000;
    public const uint BlockSize = 125;

    public bool IsSet(
        ReadOnlySpan<byte> eventFlags,
        uint eventId,
        IReadOnlyDictionary<uint, uint> blockOffsets)
    {
        ArgumentNullException.ThrowIfNull(blockOffsets);

        uint block = eventId / FlagDivisor;
        uint index = eventId % FlagDivisor;

        if (!blockOffsets.TryGetValue(block, out uint blockOffset))
        {
            throw new SaveParseException(
                SaveParseErrorCode.MissingEventFlagBlock,
                $"Event flag block {block} is not present in the block map.");
        }

        ulong byteOffset = checked((ulong)blockOffset * BlockSize + index / 8);

        if (byteOffset >= (ulong)eventFlags.Length)
        {
            throw new SaveParseException(
                SaveParseErrorCode.EventFlagOutOfBounds,
                $"Event flag {eventId} resolves outside the event flag section.",
                byteOffset > long.MaxValue ? null : (long)byteOffset);
        }

        int bitIndex = 7 - (int)(index % 8);
        return ((eventFlags[(int)byteOffset] >> bitIndex) & 1) != 0;
    }
}
