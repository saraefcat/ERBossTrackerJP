using System.Buffers.Binary;
using System.Text;
using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Save.Bnd4;
using ERBossTrackerJP.Save.Characters;
using ERBossTrackerJP.Save.Exceptions;

namespace ERBossTrackerJP.Tests.Characters;

public sealed class CharacterSlotReaderTests
{
    private readonly Bnd4Reader _bnd4Reader = new();
    private readonly CharacterSlotReader _slotReader = new();

    [Fact]
    public void Read_ReturnsOnlyActiveSlotsWithNameLevelAndOriginalIndex()
    {
        byte[] userData10 = CreateProfileSummary();
        SetProfile(userData10, 0, "褪せ人", 42, active: true);
        SetProfile(userData10, 3, "Tarnished", 150, active: true);

        Bnd4Container container = _bnd4Reader.Read(CreateBnd4(userData10));

        IReadOnlyList<CharacterSlot> slots = _slotReader.Read(container);

        Assert.Collection(
            slots,
            slot => Assert.Equal(new CharacterSlot(0, "褪せ人", 42), slot),
            slot => Assert.Equal(new CharacterSlot(3, "Tarnished", 150), slot));
    }

    [Fact]
    public void Read_ReturnsEmptyListWhenAllSlotsAreInactive()
    {
        byte[] userData10 = CreateProfileSummary();
        Bnd4Container container = _bnd4Reader.Read(CreateBnd4(userData10));

        IReadOnlyList<CharacterSlot> slots = _slotReader.Read(container);

        Assert.Empty(slots);
    }

    [Fact]
    public void Read_AcceptsSixteenCodeUnitNameWithoutInlineTerminator()
    {
        byte[] userData10 = CreateProfileSummary();
        SetProfile(userData10, 9, "1234567890123456", 713, active: true);
        Bnd4Container container = _bnd4Reader.Read(CreateBnd4(userData10));

        CharacterSlot slot = Assert.Single(_slotReader.Read(container));

        Assert.Equal(9, slot.SlotIndex);
        Assert.Equal("1234567890123456", slot.Name);
        Assert.Equal(713u, slot.Level);
    }

    [Fact]
    public void Read_DoesNotDecodeInactiveProfileData()
    {
        byte[] userData10 = CreateProfileSummary();
        int nameOffset = GetProfileOffset(4);
        userData10[nameOffset] = 0x00;
        userData10[nameOffset + 1] = 0xD8;
        Bnd4Container container = _bnd4Reader.Read(CreateBnd4(userData10));

        IReadOnlyList<CharacterSlot> slots = _slotReader.Read(container);

        Assert.Empty(slots);
    }

    [Fact]
    public void Read_RejectsContainerWithoutProfileSummaryEntry()
    {
        Bnd4Container container = _bnd4Reader.Read(CreateBnd4(null, entryCount: 10));

        var exception = Assert.Throws<SaveParseException>(() => _slotReader.Read(container));

        Assert.Equal(SaveParseErrorCode.CharacterProfileOutOfBounds, exception.ErrorCode);
    }

    [Fact]
    public void Read_RejectsTruncatedProfileSummary()
    {
        var userData10 = new byte[CharacterSlotReader.ProfileSummaryOffset + 9];
        Bnd4Container container = _bnd4Reader.Read(CreateBnd4(userData10));

        var exception = Assert.Throws<SaveParseException>(() => _slotReader.Read(container));

        Assert.Equal(SaveParseErrorCode.CharacterProfileOutOfBounds, exception.ErrorCode);
        Assert.Equal(
            container.Entries[CharacterSlotReader.ProfileSummaryEntryIndex].DataOffset +
            CharacterSlotReader.ProfileSummaryOffset,
            exception.Offset);
    }

    [Fact]
    public void Read_RejectsEmptyNameInActiveProfile()
    {
        byte[] userData10 = CreateProfileSummary();
        userData10[CharacterSlotReader.ProfileSummaryOffset] = 1;
        Bnd4Container container = _bnd4Reader.Read(CreateBnd4(userData10));

        var exception = Assert.Throws<SaveParseException>(() => _slotReader.Read(container));

        Assert.Equal(SaveParseErrorCode.InvalidCharacterName, exception.ErrorCode);
    }

    [Fact]
    public void Read_RejectsInvalidUtf16NameInActiveProfile()
    {
        byte[] userData10 = CreateProfileSummary();
        userData10[CharacterSlotReader.ProfileSummaryOffset] = 1;
        int nameOffset = GetProfileOffset(0);
        userData10[nameOffset] = 0x00;
        userData10[nameOffset + 1] = 0xD8;
        Bnd4Container container = _bnd4Reader.Read(CreateBnd4(userData10));

        var exception = Assert.Throws<SaveParseException>(() => _slotReader.Read(container));

        Assert.Equal(SaveParseErrorCode.InvalidCharacterName, exception.ErrorCode);
        Assert.Equal(
            container.Entries[CharacterSlotReader.ProfileSummaryEntryIndex].DataOffset + nameOffset,
            exception.Offset);
    }

    private static byte[] CreateProfileSummary() =>
        new byte[
            CharacterSlotReader.ProfileSummaryOffset +
            CharacterSlotReader.CharacterSlotCount +
            CharacterSlotReader.CharacterSlotCount * CharacterSlotReader.ProfileEntrySize];

    private static void SetProfile(
        byte[] userData10,
        int slotIndex,
        string name,
        uint level,
        bool active)
    {
        userData10[CharacterSlotReader.ProfileSummaryOffset + slotIndex] = active ? (byte)1 : (byte)0;
        int profileOffset = GetProfileOffset(slotIndex);
        byte[] encodedName = Encoding.Unicode.GetBytes(name);

        if (encodedName.Length > CharacterSlotReader.CharacterNameByteLength)
        {
            throw new ArgumentOutOfRangeException(nameof(name));
        }

        encodedName.CopyTo(userData10, profileOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(
            userData10.AsSpan(profileOffset + CharacterSlotReader.LevelOffset, sizeof(uint)),
            level);
    }

    private static int GetProfileOffset(int slotIndex) =>
        CharacterSlotReader.ProfileSummaryOffset +
        CharacterSlotReader.CharacterSlotCount +
        slotIndex * CharacterSlotReader.ProfileEntrySize;

    private static byte[] CreateBnd4(byte[]? userData10, int entryCount = 12)
    {
        int headersEnd = Bnd4Reader.HeaderSize + entryCount * Bnd4Reader.EntryHeaderSize;
        int namesStart = headersEnd;
        int dataStart = namesStart + entryCount * 16;
        int dataLength = entryCount;

        if (entryCount > CharacterSlotReader.ProfileSummaryEntryIndex && userData10 is not null)
        {
            dataLength += userData10.Length - 1;
        }

        var data = new byte[dataStart + dataLength];
        "BND4"u8.CopyTo(data);
        WriteUInt32(data, 0x0C, (uint)entryCount);
        WriteUInt64(data, 0x20, Bnd4Reader.EntryHeaderSize);
        WriteUInt64(data, 0x28, (ulong)dataStart);

        int nextDataOffset = dataStart;

        for (int index = 0; index < entryCount; index++)
        {
            byte[] entryData = index == CharacterSlotReader.ProfileSummaryEntryIndex && userData10 is not null
                ? userData10
                : [0];
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
