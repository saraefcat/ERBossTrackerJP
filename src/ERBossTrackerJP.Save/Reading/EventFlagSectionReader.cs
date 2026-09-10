using System.Buffers.Binary;
using ERBossTrackerJP.Save.Bnd4;
using ERBossTrackerJP.Save.Characters;
using ERBossTrackerJP.Save.Exceptions;

namespace ERBossTrackerJP.Save.Reading;

public sealed class EventFlagSectionReader
{
    public const int ChecksumSize = 0x10;
    public const int EventFlagSectionSize = 0x1BF99F;
    public const int MaximumVariableElementCount = 10_000;
    public const int MaximumVariableBlobSize = 0x10000;

    private const int HeaderSize = 0x20;
    private const int LegacyGaitemCount = 0x13FE;
    private const int CurrentGaitemCount = 0x1400;
    private const uint LegacyGaitemVersionMaximum = 81;

    public EventFlagSection Read(Bnd4Container container, int slotIndex)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentOutOfRangeException.ThrowIfNegative(slotIndex);

        if (slotIndex >= CharacterSlotReader.CharacterSlotCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotIndex),
                slotIndex,
                $"Slot index must be less than {CharacterSlotReader.CharacterSlotCount}.");
        }

        if (slotIndex >= container.Entries.Count)
        {
            throw new SaveParseException(
                SaveParseErrorCode.EventFlagSectionNotFound,
                $"The BND4 container has {container.Entries.Count} entries; character slot {slotIndex} is not present.");
        }

        Bnd4Entry entry = container.Entries[slotIndex];
        ReadOnlyMemory<byte> entryData = container.GetEntryData(slotIndex);

        if (entryData.Length < ChecksumSize)
        {
            throw new SaveParseException(
                SaveParseErrorCode.EventFlagSectionNotFound,
                $"Character slot {slotIndex} is shorter than its {ChecksumSize}-byte checksum.",
                entry.DataOffset);
        }

        ReadOnlyMemory<byte> slotData = entryData[ChecksumSize..];
        long absoluteSlotDataOffset = (long)entry.DataOffset + ChecksumSize;
        int eventFlagOffset = FindEventFlagOffset(slotData.Span, absoluteSlotDataOffset);

        return new EventFlagSection(
            slotData.Slice(eventFlagOffset, EventFlagSectionSize),
            eventFlagOffset);
    }

    private static int FindEventFlagOffset(
        ReadOnlySpan<byte> slotData,
        long absoluteSlotDataOffset)
    {
        var cursor = new SlotCursor(slotData, absoluteSlotDataOffset);
        uint version = cursor.ReadUInt32("slot version");

        if (version == 0)
        {
            throw new SaveParseException(
                SaveParseErrorCode.EmptyCharacterSlot,
                "The selected character slot is empty.",
                absoluteSlotDataOffset);
        }

        cursor.Skip(HeaderSize - sizeof(uint), "slot header");

        int gaitemCount = version <= LegacyGaitemVersionMaximum
            ? LegacyGaitemCount
            : CurrentGaitemCount;

        for (int index = 0; index < gaitemCount; index++)
        {
            uint handle = cursor.ReadUInt32($"Gaitem map entry {index} handle");
            cursor.Skip(sizeof(uint), $"Gaitem map entry {index} item ID");

            uint handleType = handle & 0xF0000000;

            if (handle != 0 && handleType != 0xC0000000)
            {
                cursor.Skip(8, $"Gaitem map entry {index} extended fields");

                if (handleType == 0x80000000)
                {
                    cursor.Skip(5, $"Gaitem map entry {index} gem fields");
                }
            }
        }

        cursor.Skip(0x1B0, "PlayerGameData");
        cursor.Skip(13 * 16, "SPEffects");
        cursor.Skip(88, "EquippedItemsEquipIndex");
        cursor.Skip(28, "ActiveWeaponSlotsAndArmStyle");
        cursor.Skip(88, "EquippedItemsItemIds");
        cursor.Skip(88, "EquippedItemsGaitemHandles");
        cursor.Skip(4 + 0xA80 * 12 + 4 + 0x180 * 12 + 4 + 4, "held inventory");
        cursor.Skip(14 * 8 + 4, "EquippedSpells");
        cursor.Skip(10 * 8 + 4 + 6 * 8 + 4 + 4, "EquippedItems");
        cursor.Skip(6 * 4, "EquippedGestures");

        uint projectileCount = cursor.ReadBoundedValue(
            MaximumVariableElementCount,
            "AcquiredProjectiles count");
        cursor.SkipElements(projectileCount, 8, "AcquiredProjectiles");

        cursor.Skip(39 * 4, "EquippedArmamentsAndItems");
        cursor.Skip(3 * 4, "EquippedPhysics");
        cursor.Skip(0x12F, "FaceData");
        cursor.Skip(4 + 0x780 * 12 + 4 + 0x80 * 12 + 4 + 4, "storage inventory");
        cursor.Skip(64 * 4, "Gestures");

        uint regionCount = cursor.ReadBoundedValue(
            MaximumVariableElementCount,
            "Unlocked Regions count");
        cursor.SkipElements(regionCount, 4, "Unlocked Regions");

        cursor.Skip(40, "RideGameData");
        cursor.Skip(1, "control byte");
        cursor.Skip(0x44, "BloodStain");
        cursor.Skip(8, "GameDataMan fields");
        cursor.SkipSizedBlob(MaximumVariableBlobSize, "MenuProfileSaveLoad");
        cursor.Skip(0x34, "TrophyEquipData");
        cursor.Skip(8 + 7000 * 16, "GaitemGameData");
        cursor.SkipSizedBlob(MaximumVariableBlobSize, "TutorialData");
        cursor.Skip(3, "GameMan fields");
        cursor.Skip(4, "total death count");
        cursor.Skip(22, "online and character state");

        int eventFlagOffset = cursor.Position;
        cursor.Skip(EventFlagSectionSize, "event flags");
        return eventFlagOffset;
    }

    private ref struct SlotCursor
    {
        private readonly ReadOnlySpan<byte> _data;
        private readonly long _absoluteBaseOffset;

        public SlotCursor(ReadOnlySpan<byte> data, long absoluteBaseOffset)
        {
            _data = data;
            _absoluteBaseOffset = absoluteBaseOffset;
        }

        public int Position { get; private set; }

        public uint ReadUInt32(string fieldName)
        {
            EnsureAvailable(sizeof(uint), fieldName);
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(
                _data.Slice(Position, sizeof(uint)));
            Position += sizeof(uint);
            return value;
        }

        public uint ReadBoundedValue(uint maximum, string fieldName)
        {
            int valueOffset = Position;
            uint value = ReadUInt32(fieldName);

            if (value > maximum)
            {
                throw InvalidValue(
                    valueOffset,
                    $"{fieldName} is {value}; the supported maximum is {maximum}.");
            }

            return value;
        }

        public void Skip(int byteCount, string sectionName)
        {
            EnsureAvailable(byteCount, sectionName);
            Position += byteCount;
        }

        public void SkipElements(uint count, int elementSize, string sectionName)
        {
            ulong byteCount = (ulong)count * (uint)elementSize;

            if (byteCount > int.MaxValue)
            {
                throw InvalidValue(
                    Position,
                    $"{sectionName} requires {byteCount} bytes, which exceeds the supported size.");
            }

            Skip((int)byteCount, sectionName);
        }

        public void SkipSizedBlob(uint maximumSize, string sectionName)
        {
            Skip(sizeof(uint), $"{sectionName} header");
            uint size = ReadBoundedValue(maximumSize, $"{sectionName} size");
            Skip((int)size, sectionName);
        }

        private void EnsureAvailable(int byteCount, string sectionName)
        {
            int remaining = _data.Length - Position;

            if (byteCount > remaining)
            {
                throw new SaveParseException(
                    SaveParseErrorCode.EventFlagSectionNotFound,
                    $"Cannot locate the event flag section: {sectionName} needs {byteCount} bytes, but only {remaining} remain.",
                    _absoluteBaseOffset + Position);
            }
        }

        private SaveParseException InvalidValue(int relativeOffset, string message) =>
            new(
                SaveParseErrorCode.EventFlagSectionNotFound,
                $"Cannot locate the event flag section: {message}",
                _absoluteBaseOffset + relativeOffset);
    }
}
