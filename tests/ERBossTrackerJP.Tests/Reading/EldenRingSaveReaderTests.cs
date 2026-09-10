using System.Buffers.Binary;
using System.Text;
using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Save.Bnd4;
using ERBossTrackerJP.Save.Characters;
using ERBossTrackerJP.Save.Exceptions;
using ERBossTrackerJP.Save.Reading;

namespace ERBossTrackerJP.Tests.Reading;

public sealed class EldenRingSaveReaderTests
{
    private readonly EldenRingSaveReader _reader = new();

    [Fact]
    public void ReadCharacterSlots_ParsesCompleteSaveThroughPublicApi()
    {
        byte[][] entries = CreateEntries();
        entries[CharacterSlotReader.ProfileSummaryEntryIndex] = CreateProfileSummary(
            slotIndex: 3,
            name: "褪せ人",
            level: 150);

        IReadOnlyList<CharacterSlot> slots = _reader.ReadCharacterSlots(CreateBnd4(entries));

        CharacterSlot slot = Assert.Single(slots);
        Assert.Equal(new CharacterSlot(3, "褪せ人", 150), slot);
    }

    [Fact]
    public void ReadEventFlags_PropagatesDetailedSlotParseError()
    {
        byte[][] entries = CreateEntries();
        entries[0] = new byte[EventFlagSectionReader.ChecksumSize + 32];

        var exception = Assert.Throws<SaveParseException>(() =>
            _reader.ReadEventFlags(CreateBnd4(entries), 0));

        Assert.Equal(SaveParseErrorCode.EmptyCharacterSlot, exception.ErrorCode);
        Assert.NotNull(exception.Offset);
    }

    [Fact]
    public void ReadCharacterSlots_RejectsBnd4WithUnexpectedEntryCount()
    {
        byte[][] entries = CreateEntries(EldenRingSaveReader.ExpectedEntryCount - 1);

        var exception = Assert.Throws<SaveParseException>(() =>
            _reader.ReadCharacterSlots(CreateBnd4(entries)));

        Assert.Equal(SaveParseErrorCode.InvalidBnd4Header, exception.ErrorCode);
        Assert.Equal(Bnd4Reader.FileCountOffset, exception.Offset);
    }

    [Fact]
    public void ReadCharacterSlots_PreservesBnd4ParseError()
    {
        var exception = Assert.Throws<SaveParseException>(() =>
            _reader.ReadCharacterSlots(ReadOnlyMemory<byte>.Empty));

        Assert.Equal(SaveParseErrorCode.Bnd4HeaderOutOfBounds, exception.ErrorCode);
    }

    private static byte[][] CreateEntries(
        int count = EldenRingSaveReader.ExpectedEntryCount) =>
        Enumerable.Range(0, count).Select(_ => new byte[1]).ToArray();

    private static byte[] CreateProfileSummary(int slotIndex, string name, uint level)
    {
        var data = new byte[
            CharacterSlotReader.ProfileSummaryOffset +
            CharacterSlotReader.CharacterSlotCount +
            CharacterSlotReader.CharacterSlotCount * CharacterSlotReader.ProfileEntrySize];
        data[CharacterSlotReader.ProfileSummaryOffset + slotIndex] = 1;
        int profileOffset =
            CharacterSlotReader.ProfileSummaryOffset +
            CharacterSlotReader.CharacterSlotCount +
            slotIndex * CharacterSlotReader.ProfileEntrySize;
        Encoding.Unicode.GetBytes(name).CopyTo(data, profileOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(
            data.AsSpan(profileOffset + CharacterSlotReader.LevelOffset, sizeof(uint)),
            level);
        return data;
    }

    private static byte[] CreateBnd4(IReadOnlyList<byte[]> entries)
    {
        int headersEnd = Bnd4Reader.HeaderSize + entries.Count * Bnd4Reader.EntryHeaderSize;
        int namesStart = headersEnd;
        int dataStart = namesStart + entries.Count * 16;
        int totalEntrySize = entries.Sum(entry => entry.Length);
        var data = new byte[dataStart + totalEntrySize];

        "BND4"u8.CopyTo(data);
        WriteUInt32(data, Bnd4Reader.FileCountOffset, (uint)entries.Count);
        WriteUInt64(data, 0x20, Bnd4Reader.EntryHeaderSize);
        WriteUInt64(data, 0x28, (ulong)dataStart);

        int nextDataOffset = dataStart;

        for (int index = 0; index < entries.Count; index++)
        {
            byte[] entryData = entries[index];
            int headerOffset = Bnd4Reader.HeaderSize + index * Bnd4Reader.EntryHeaderSize;
            int nameOffset = namesStart + index * 16;

            WriteUInt64(data, headerOffset + 0x08, (ulong)entryData.Length);
            WriteUInt32(data, headerOffset + 0x10, (uint)nextDataOffset);
            WriteUInt32(data, headerOffset + 0x14, (uint)nameOffset);
            Encoding.ASCII.GetBytes($"USER_DATA_{index:000}\0", data.AsSpan(nameOffset));
            entryData.CopyTo(data, nextDataOffset);
            nextDataOffset += entryData.Length;
        }

        return data;
    }

    private static void WriteUInt32(byte[] data, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, sizeof(uint)), value);

    private static void WriteUInt64(byte[] data, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset, sizeof(ulong)), value);
}
