using System.Buffers.Binary;
using ERBossTrackerJP.Save.Bnd4;
using ERBossTrackerJP.Save.Exceptions;
using ERBossTrackerJP.Save.Reading;

namespace ERBossTrackerJP.Tests.Reading;

public sealed class EventFlagSectionReaderTests
{
    private const int Bnd4EntryDataOffset =
        Bnd4Reader.HeaderSize + Bnd4Reader.EntryHeaderSize + 16;

    private readonly Bnd4Reader _bnd4Reader = new();
    private readonly EventFlagSectionReader _reader = new();

    [Fact]
    public void Read_ReturnsCurrentVersionEventFlagsAndSlotRelativeOffset()
    {
        BuiltSlot slot = BuildValidSlot(version: 82, totalDeathCount: 1_248);
        Bnd4Container container = ReadContainer(slot.Bytes);

        EventFlagSection section = _reader.Read(container, 0);

        Assert.Equal(slot.EventFlagOffset, section.Offset);
        Assert.Equal(216712, section.Offset);
        Assert.Equal(EventFlagSectionReader.EventFlagSectionSize, section.Bytes.Length);
        Assert.Equal(1_248u, section.TotalDeathCount);
        Assert.Equal(0xA5, section.Bytes.Span[0]);
        Assert.Equal(0x5A, section.Bytes.Span[^1]);
    }

    [Fact]
    public void Read_UsesLegacyGaitemCountForVersion81()
    {
        BuiltSlot slot = BuildValidSlot(version: 81);
        Bnd4Container container = ReadContainer(slot.Bytes);

        EventFlagSection section = _reader.Read(container, 0);

        Assert.Equal(slot.EventFlagOffset, section.Offset);
        Assert.Equal(216696, section.Offset);
        Assert.Equal(EventFlagSectionReader.EventFlagSectionSize, section.Bytes.Length);
    }

    [Fact]
    public void Read_RejectsEmptyCharacterSlot()
    {
        var entryData = new byte[EventFlagSectionReader.ChecksumSize + 32];
        Bnd4Container container = ReadContainer(entryData);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(container, 0));

        Assert.Equal(SaveParseErrorCode.EmptyCharacterSlot, exception.ErrorCode);
        Assert.Equal(
            Bnd4EntryDataOffset + EventFlagSectionReader.ChecksumSize,
            exception.Offset);
    }

    [Fact]
    public void Read_RejectsEntryShorterThanChecksum()
    {
        Bnd4Container container = ReadContainer(
            new byte[EventFlagSectionReader.ChecksumSize - 1]);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(container, 0));

        Assert.Equal(SaveParseErrorCode.EventFlagSectionNotFound, exception.ErrorCode);
        Assert.Equal(Bnd4EntryDataOffset, exception.Offset);
    }

    [Fact]
    public void Read_RejectsMissingCharacterEntry()
    {
        Bnd4Container container = ReadContainer(new byte[EventFlagSectionReader.ChecksumSize]);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(container, 1));

        Assert.Equal(SaveParseErrorCode.EventFlagSectionNotFound, exception.ErrorCode);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public void Read_RejectsSlotIndexOutsideCharacterRange(int slotIndex)
    {
        Bnd4Container container = ReadContainer(new byte[EventFlagSectionReader.ChecksumSize]);

        Assert.Throws<ArgumentOutOfRangeException>(() => _reader.Read(container, slotIndex));
    }

    [Fact]
    public void Read_RejectsExcessiveProjectileCountBeforeMultiplication()
    {
        BuiltSlot slot = BuildSlot(projectileCount: uint.MaxValue, stopAfter: StopAfter.Projectiles);
        Bnd4Container container = ReadContainer(slot.Bytes);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(container, 0));

        Assert.Equal(SaveParseErrorCode.EventFlagSectionNotFound, exception.ErrorCode);
        Assert.Equal(AbsoluteOffset(slot.ProjectileCountOffset), exception.Offset);
    }

    [Fact]
    public void Read_RejectsExcessiveRegionCountBeforeMultiplication()
    {
        BuiltSlot slot = BuildSlot(regionCount: uint.MaxValue, stopAfter: StopAfter.Regions);
        Bnd4Container container = ReadContainer(slot.Bytes);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(container, 0));

        Assert.Equal(SaveParseErrorCode.EventFlagSectionNotFound, exception.ErrorCode);
        Assert.Equal(AbsoluteOffset(slot.RegionCountOffset), exception.Offset);
    }

    [Fact]
    public void Read_RejectsExcessiveMenuDataSize()
    {
        BuiltSlot slot = BuildSlot(
            menuSize: EventFlagSectionReader.MaximumVariableBlobSize + 1u,
            stopAfter: StopAfter.Menu);
        Bnd4Container container = ReadContainer(slot.Bytes);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(container, 0));

        Assert.Equal(SaveParseErrorCode.EventFlagSectionNotFound, exception.ErrorCode);
        Assert.Equal(AbsoluteOffset(slot.MenuSizeOffset), exception.Offset);
    }

    [Fact]
    public void Read_RejectsExcessiveTutorialDataSize()
    {
        BuiltSlot slot = BuildSlot(
            tutorialSize: EventFlagSectionReader.MaximumVariableBlobSize + 1u,
            stopAfter: StopAfter.Tutorial);
        Bnd4Container container = ReadContainer(slot.Bytes);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(container, 0));

        Assert.Equal(SaveParseErrorCode.EventFlagSectionNotFound, exception.ErrorCode);
        Assert.Equal(AbsoluteOffset(slot.TutorialSizeOffset), exception.Offset);
    }

    [Fact]
    public void Read_RejectsTruncatedEventFlagSection()
    {
        BuiltSlot slot = BuildValidSlot(version: 82);
        Bnd4Container container = ReadContainer(slot.Bytes[..^1]);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(container, 0));

        Assert.Equal(SaveParseErrorCode.EventFlagSectionNotFound, exception.ErrorCode);
        Assert.Equal(AbsoluteOffset(slot.EventFlagOffset), exception.Offset);
    }

    private static long AbsoluteOffset(int slotRelativeOffset) =>
        Bnd4EntryDataOffset + EventFlagSectionReader.ChecksumSize + slotRelativeOffset;

    private Bnd4Container ReadContainer(byte[] entryData) =>
        _bnd4Reader.Read(CreateBnd4(entryData));

    private static BuiltSlot BuildValidSlot(
        uint version,
        uint totalDeathCount = 0) =>
        BuildSlot(
            version: version,
            totalDeathCount: totalDeathCount,
            stopAfter: StopAfter.EventFlags);

    private static BuiltSlot BuildSlot(
        uint version = 82,
        uint projectileCount = 3,
        uint regionCount = 2,
        uint menuSize = 17,
        uint tutorialSize = 9,
        uint totalDeathCount = 0,
        StopAfter stopAfter = StopAfter.EventFlags)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        Skip(stream, EventFlagSectionReader.ChecksumSize);
        writer.Write(version);
        Skip(stream, 32 - sizeof(uint));

        int gaitemCount = version <= 81 ? 0x13FE : 0x1400;

        for (int index = 0; index < gaitemCount; index++)
        {
            uint handle = index switch
            {
                1 => 0xC0000001,
                2 => 0x10000001,
                3 => 0x80000001,
                _ => 0,
            };

            writer.Write(handle);
            writer.Write(0u);
            uint handleType = handle & 0xF0000000;

            if (handle != 0 && handleType != 0xC0000000)
            {
                Skip(stream, 8);

                if (handleType == 0x80000000)
                {
                    Skip(stream, 5);
                }
            }
        }

        Skip(stream, 0x1B0);
        Skip(stream, 13 * 16);
        Skip(stream, 88 + 28 + 88 + 88);
        Skip(stream, 4 + 0xA80 * 12 + 4 + 0x180 * 12 + 4 + 4);
        Skip(stream, 14 * 8 + 4);
        Skip(stream, 10 * 8 + 4 + 6 * 8 + 4 + 4);
        Skip(stream, 6 * 4);

        int projectileCountOffset = SlotPosition(stream);
        writer.Write(projectileCount);

        if (stopAfter == StopAfter.Projectiles)
        {
            return BuildResult(stream, projectileCountOffset: projectileCountOffset);
        }

        Skip(stream, checked((int)projectileCount * 8));
        Skip(stream, 39 * 4 + 3 * 4 + 0x12F);
        Skip(stream, 4 + 0x780 * 12 + 4 + 0x80 * 12 + 4 + 4);
        Skip(stream, 64 * 4);

        int regionCountOffset = SlotPosition(stream);
        writer.Write(regionCount);

        if (stopAfter == StopAfter.Regions)
        {
            return BuildResult(
                stream,
                projectileCountOffset,
                regionCountOffset: regionCountOffset);
        }

        Skip(stream, checked((int)regionCount * 4));
        Skip(stream, 40 + 1 + 0x44 + 8);
        writer.Write(0u);
        int menuSizeOffset = SlotPosition(stream);
        writer.Write(menuSize);

        if (stopAfter == StopAfter.Menu)
        {
            return BuildResult(
                stream,
                projectileCountOffset,
                regionCountOffset,
                menuSizeOffset: menuSizeOffset);
        }

        Skip(stream, checked((int)menuSize));
        Skip(stream, 0x34 + 8 + 7000 * 16);
        writer.Write(0u);
        int tutorialSizeOffset = SlotPosition(stream);
        writer.Write(tutorialSize);

        if (stopAfter == StopAfter.Tutorial)
        {
            return BuildResult(
                stream,
                projectileCountOffset,
                regionCountOffset,
                menuSizeOffset,
                tutorialSizeOffset: tutorialSizeOffset);
        }

        Skip(stream, checked((int)tutorialSize));
        Skip(stream, 3);
        writer.Write(totalDeathCount);
        Skip(stream, 22);
        int eventFlagOffset = SlotPosition(stream);
        Skip(stream, EventFlagSectionReader.EventFlagSectionSize);
        stream.Position = EventFlagSectionReader.ChecksumSize + eventFlagOffset;
        writer.Write((byte)0xA5);
        stream.Position = stream.Length - 1;
        writer.Write((byte)0x5A);

        return BuildResult(
            stream,
            projectileCountOffset,
            regionCountOffset,
            menuSizeOffset,
            tutorialSizeOffset,
            eventFlagOffset);
    }

    private static BuiltSlot BuildResult(
        MemoryStream stream,
        int projectileCountOffset = 0,
        int regionCountOffset = 0,
        int menuSizeOffset = 0,
        int tutorialSizeOffset = 0,
        int eventFlagOffset = 0) =>
        new(
            stream.ToArray(),
            projectileCountOffset,
            regionCountOffset,
            menuSizeOffset,
            tutorialSizeOffset,
            eventFlagOffset);

    private static int SlotPosition(MemoryStream stream) =>
        checked((int)stream.Position - EventFlagSectionReader.ChecksumSize);

    private static void Skip(MemoryStream stream, int byteCount)
    {
        long end = checked(stream.Position + byteCount);

        if (end > stream.Length)
        {
            stream.SetLength(end);
        }

        stream.Position = end;
    }

    private static byte[] CreateBnd4(byte[] entryData)
    {
        int nameOffset = Bnd4Reader.HeaderSize + Bnd4Reader.EntryHeaderSize;
        int dataOffset = nameOffset + 16;
        var data = new byte[dataOffset + entryData.Length];

        "BND4"u8.CopyTo(data);
        WriteUInt32(data, 0x0C, 1);
        WriteUInt64(data, 0x20, Bnd4Reader.EntryHeaderSize);
        WriteUInt64(data, 0x28, (ulong)dataOffset);
        WriteUInt64(data, Bnd4Reader.HeaderSize + 0x08, (ulong)entryData.Length);
        WriteUInt32(data, Bnd4Reader.HeaderSize + 0x10, (uint)dataOffset);
        WriteUInt32(data, Bnd4Reader.HeaderSize + 0x14, (uint)nameOffset);
        "USER_DATA_000\0"u8.CopyTo(data.AsSpan(nameOffset));
        entryData.CopyTo(data, dataOffset);
        return data;
    }

    private static void WriteUInt32(byte[] data, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, sizeof(uint)), value);

    private static void WriteUInt64(byte[] data, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset, sizeof(ulong)), value);

    private enum StopAfter
    {
        Projectiles,
        Regions,
        Menu,
        Tutorial,
        EventFlags,
    }

    private sealed record BuiltSlot(
        byte[] Bytes,
        int ProjectileCountOffset,
        int RegionCountOffset,
        int MenuSizeOffset,
        int TutorialSizeOffset,
        int EventFlagOffset);
}
