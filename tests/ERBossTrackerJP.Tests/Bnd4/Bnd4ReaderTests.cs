using System.Buffers.Binary;
using System.Text;
using ERBossTrackerJP.Save.Bnd4;
using ERBossTrackerJP.Save.Exceptions;

namespace ERBossTrackerJP.Tests.Bnd4;

public sealed class Bnd4ReaderTests
{
    private readonly Bnd4Reader _reader = new();

    [Fact]
    public void Read_ParsesValidatedEntriesAndReturnsTheirData()
    {
        byte[] data = CreateValidBnd4(2);

        Bnd4Container container = _reader.Read(data);

        Assert.Equal(2, container.Entries.Count);
        Assert.Equal(4, container.Entries[0].DataSize);
        Assert.Equal([1, 2, 3, 4], container.GetEntryData(0).ToArray());
        Assert.Equal([5, 6, 7, 8], container.GetEntryData(1).ToArray());
    }

    [Fact]
    public void Read_RejectsDataShorterThanHeader()
    {
        var exception = Assert.Throws<SaveParseException>(() =>
            _reader.Read(new byte[Bnd4Reader.HeaderSize - 1]));

        Assert.Equal(SaveParseErrorCode.Bnd4HeaderOutOfBounds, exception.ErrorCode);
    }

    [Fact]
    public void Read_RejectsInvalidMagic()
    {
        byte[] data = CreateValidBnd4(1);
        data[0] = (byte)'X';

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(data));

        Assert.Equal(SaveParseErrorCode.InvalidBnd4Magic, exception.ErrorCode);
        Assert.Equal(0, exception.Offset);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData((uint)Bnd4Reader.MaximumEntryCount + 1)]
    public void Read_RejectsUnsupportedFileCountBeforeAllocating(uint fileCount)
    {
        byte[] data = CreateHeaderOnlyBnd4();
        WriteUInt32(data, 0x0C, fileCount);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(data));

        Assert.Equal(SaveParseErrorCode.InvalidBnd4Header, exception.ErrorCode);
        Assert.Equal(0x0C, exception.Offset);
    }

    [Fact]
    public void Read_RejectsEntryHeaderTableOutsideInput()
    {
        byte[] data = CreateHeaderOnlyBnd4();
        WriteUInt32(data, 0x0C, 2);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(data));

        Assert.Equal(SaveParseErrorCode.Bnd4HeaderOutOfBounds, exception.ErrorCode);
    }

    [Fact]
    public void Read_RejectsUnexpectedEntryHeaderSize()
    {
        byte[] data = CreateValidBnd4(1);
        WriteUInt64(data, 0x20, 0x18);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(data));

        Assert.Equal(SaveParseErrorCode.InvalidBnd4Header, exception.ErrorCode);
        Assert.Equal(0x20, exception.Offset);
    }

    [Fact]
    public void Read_RejectsDataStartBeforeEntryHeadersEnd()
    {
        byte[] data = CreateValidBnd4(1);
        WriteUInt64(data, 0x28, Bnd4Reader.HeaderSize);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(data));

        Assert.Equal(SaveParseErrorCode.InvalidBnd4Header, exception.ErrorCode);
        Assert.Equal(0x28, exception.Offset);
    }

    [Fact]
    public void Read_RejectsEntryDataOffsetOutsideInput()
    {
        byte[] data = CreateValidBnd4(1);
        WriteUInt32(data, Bnd4Reader.HeaderSize + 0x10, uint.MaxValue);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(data));

        Assert.Equal(SaveParseErrorCode.Bnd4EntryOutOfBounds, exception.ErrorCode);
        Assert.Equal(Bnd4Reader.HeaderSize + 0x10, exception.Offset);
    }

    [Fact]
    public void Read_RejectsEntryDataOffsetBeforeDataArea()
    {
        byte[] data = CreateValidBnd4(1);
        WriteUInt32(data, Bnd4Reader.HeaderSize + 0x10, Bnd4Reader.HeaderSize);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(data));

        Assert.Equal(SaveParseErrorCode.Bnd4EntryOutOfBounds, exception.ErrorCode);
        Assert.Equal(Bnd4Reader.HeaderSize + 0x10, exception.Offset);
    }

    [Fact]
    public void Read_RejectsEntrySizeOutsideInputWithoutOverflowing()
    {
        byte[] data = CreateValidBnd4(1);
        WriteUInt64(data, Bnd4Reader.HeaderSize + 0x08, ulong.MaxValue);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(data));

        Assert.Equal(SaveParseErrorCode.Bnd4EntryOutOfBounds, exception.ErrorCode);
        Assert.Equal(Bnd4Reader.HeaderSize + 0x08, exception.Offset);
    }

    [Fact]
    public void Read_RejectsNameOffsetOutsideInput()
    {
        byte[] data = CreateValidBnd4(1);
        WriteUInt32(data, Bnd4Reader.HeaderSize + 0x14, (uint)data.Length);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(data));

        Assert.Equal(SaveParseErrorCode.Bnd4EntryOutOfBounds, exception.ErrorCode);
        Assert.Equal(Bnd4Reader.HeaderSize + 0x14, exception.Offset);
    }

    [Fact]
    public void Read_RejectsNameOffsetOutsideNameTable()
    {
        byte[] data = CreateValidBnd4(1);
        uint dataStart = (uint)(Bnd4Reader.HeaderSize + Bnd4Reader.EntryHeaderSize + 16);
        WriteUInt32(data, Bnd4Reader.HeaderSize + 0x14, dataStart);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(data));

        Assert.Equal(SaveParseErrorCode.Bnd4EntryOutOfBounds, exception.ErrorCode);
        Assert.Equal(Bnd4Reader.HeaderSize + 0x14, exception.Offset);
    }

    private static byte[] CreateHeaderOnlyBnd4()
    {
        var data = new byte[Bnd4Reader.HeaderSize];
        "BND4"u8.CopyTo(data);
        WriteUInt64(data, 0x20, Bnd4Reader.EntryHeaderSize);
        WriteUInt64(data, 0x28, Bnd4Reader.HeaderSize);
        return data;
    }

    private static byte[] CreateValidBnd4(int entryCount)
    {
        int headersEnd = Bnd4Reader.HeaderSize + entryCount * Bnd4Reader.EntryHeaderSize;
        int namesStart = headersEnd;
        int dataStart = namesStart + entryCount * 16;
        var data = new byte[dataStart + entryCount * 4];

        "BND4"u8.CopyTo(data);
        WriteUInt32(data, 0x0C, (uint)entryCount);
        WriteUInt64(data, 0x20, Bnd4Reader.EntryHeaderSize);
        WriteUInt64(data, 0x28, (ulong)dataStart);

        for (int index = 0; index < entryCount; index++)
        {
            int headerOffset = Bnd4Reader.HeaderSize + index * Bnd4Reader.EntryHeaderSize;
            int nameOffset = namesStart + index * 16;
            int entryDataOffset = dataStart + index * 4;

            WriteUInt64(data, headerOffset + 0x08, 4);
            WriteUInt32(data, headerOffset + 0x10, (uint)entryDataOffset);
            WriteUInt32(data, headerOffset + 0x14, (uint)nameOffset);

            Encoding.ASCII.GetBytes($"USER_DATA_{index:000}\0", data.AsSpan(nameOffset));

            for (int dataIndex = 0; dataIndex < 4; dataIndex++)
            {
                data[entryDataOffset + dataIndex] = (byte)(index * 4 + dataIndex + 1);
            }
        }

        return data;
    }

    private static void WriteUInt32(byte[] data, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, sizeof(uint)), value);

    private static void WriteUInt64(byte[] data, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset, sizeof(ulong)), value);
}
