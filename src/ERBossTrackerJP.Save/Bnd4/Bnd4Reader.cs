using System.Buffers.Binary;
using ERBossTrackerJP.Save.Exceptions;

namespace ERBossTrackerJP.Save.Bnd4;

public sealed class Bnd4Reader
{
    public const int HeaderSize = 0x40;
    public const int EntryHeaderSize = 0x20;
    public const int MaximumEntryCount = 1024;

    private const int FileCountOffset = 0x0C;
    private const int DeclaredEntryHeaderSizeOffset = 0x20;
    private const int DataStartOffset = 0x28;
    private const int EntrySizeOffset = 0x08;
    private const int EntryDataOffset = 0x10;
    private const int EntryNameOffset = 0x14;

    private static ReadOnlySpan<byte> Magic => "BND4"u8;

    public Bnd4Container Read(ReadOnlyMemory<byte> data)
    {
        if (data.Length < HeaderSize)
        {
            throw new SaveParseException(
                SaveParseErrorCode.Bnd4HeaderOutOfBounds,
                $"The save contains {data.Length} bytes; a BND4 header requires {HeaderSize} bytes.",
                data.Length);
        }

        ReadOnlySpan<byte> span = data.Span;

        if (!span[..Magic.Length].SequenceEqual(Magic))
        {
            throw new SaveParseException(
                SaveParseErrorCode.InvalidBnd4Magic,
                "The save does not start with the BND4 magic value.",
                0);
        }

        uint fileCountValue = BinaryPrimitives.ReadUInt32LittleEndian(
            span.Slice(FileCountOffset, sizeof(uint)));

        if (fileCountValue is 0 or > MaximumEntryCount)
        {
            throw new SaveParseException(
                SaveParseErrorCode.InvalidBnd4Header,
                $"The BND4 file count {fileCountValue} is outside the supported range 1-{MaximumEntryCount}.",
                FileCountOffset);
        }

        ulong declaredEntryHeaderSize = BinaryPrimitives.ReadUInt64LittleEndian(
            span.Slice(DeclaredEntryHeaderSizeOffset, sizeof(ulong)));

        if (declaredEntryHeaderSize != EntryHeaderSize)
        {
            throw new SaveParseException(
                SaveParseErrorCode.InvalidBnd4Header,
                $"The BND4 entry header size is {declaredEntryHeaderSize}; expected {EntryHeaderSize}.",
                DeclaredEntryHeaderSizeOffset);
        }

        int fileCount = (int)fileCountValue;
        ulong entryHeadersEnd = HeaderSize + (ulong)fileCount * EntryHeaderSize;

        if (entryHeadersEnd > (ulong)data.Length)
        {
            throw new SaveParseException(
                SaveParseErrorCode.Bnd4HeaderOutOfBounds,
                "The BND4 entry header table extends beyond the save data.",
                HeaderSize);
        }

        ulong dataStartValue = BinaryPrimitives.ReadUInt64LittleEndian(
            span.Slice(DataStartOffset, sizeof(ulong)));

        if (dataStartValue < entryHeadersEnd || dataStartValue > (ulong)data.Length)
        {
            throw new SaveParseException(
                SaveParseErrorCode.InvalidBnd4Header,
                $"The BND4 data start offset {dataStartValue} is outside the valid range.",
                DataStartOffset);
        }

        var entries = new Bnd4Entry[fileCount];

        for (int index = 0; index < fileCount; index++)
        {
            int headerOffset = HeaderSize + index * EntryHeaderSize;
            ulong entrySizeValue = BinaryPrimitives.ReadUInt64LittleEndian(
                span.Slice(headerOffset + EntrySizeOffset, sizeof(ulong)));
            uint entryDataOffsetValue = BinaryPrimitives.ReadUInt32LittleEndian(
                span.Slice(headerOffset + EntryDataOffset, sizeof(uint)));
            uint entryNameOffsetValue = BinaryPrimitives.ReadUInt32LittleEndian(
                span.Slice(headerOffset + EntryNameOffset, sizeof(uint)));

            ValidateEntryDataRange(
                data.Length,
                dataStartValue,
                index,
                headerOffset,
                entryDataOffsetValue,
                entrySizeValue);

            if (entryNameOffsetValue < entryHeadersEnd || entryNameOffsetValue >= dataStartValue)
            {
                throw new SaveParseException(
                    SaveParseErrorCode.Bnd4EntryOutOfBounds,
                    $"BND4 entry {index} has a name offset {entryNameOffsetValue} outside the name table.",
                    headerOffset + EntryNameOffset);
            }

            entries[index] = new Bnd4Entry(
                index,
                (int)entryDataOffsetValue,
                (int)entrySizeValue,
                (int)entryNameOffsetValue);
        }

        return new Bnd4Container(data, (int)dataStartValue, entries);
    }

    private static void ValidateEntryDataRange(
        int dataLength,
        ulong dataStartOffset,
        int entryIndex,
        int headerOffset,
        uint dataOffset,
        ulong dataSize)
    {
        if ((ulong)dataOffset < dataStartOffset || (ulong)dataOffset > (ulong)dataLength)
        {
            throw new SaveParseException(
                SaveParseErrorCode.Bnd4EntryOutOfBounds,
                $"BND4 entry {entryIndex} has a data offset {dataOffset} outside the data area.",
                headerOffset + EntryDataOffset);
        }

        int remainingLength = dataLength - (int)dataOffset;

        if (dataSize > (ulong)remainingLength)
        {
            throw new SaveParseException(
                SaveParseErrorCode.Bnd4EntryOutOfBounds,
                $"BND4 entry {entryIndex} requires {dataSize} bytes, but only {remainingLength} remain.",
                headerOffset + EntrySizeOffset);
        }
    }
}
