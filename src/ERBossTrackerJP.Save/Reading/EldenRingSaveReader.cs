using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Save.Bnd4;
using ERBossTrackerJP.Save.Characters;
using ERBossTrackerJP.Save.Exceptions;

namespace ERBossTrackerJP.Save.Reading;

public sealed class EldenRingSaveReader : IEldenRingSaveReader
{
    public const int ExpectedEntryCount = 12;

    private readonly Bnd4Reader _bnd4Reader = new();
    private readonly CharacterSlotReader _characterSlotReader = new();
    private readonly EventFlagSectionReader _eventFlagSectionReader = new();

    public IReadOnlyList<CharacterSlot> ReadCharacterSlots(ReadOnlyMemory<byte> saveData)
    {
        Bnd4Container container = ReadContainer(saveData);
        return _characterSlotReader.Read(container);
    }

    public EventFlagSection ReadEventFlags(ReadOnlyMemory<byte> saveData, int slotIndex)
    {
        Bnd4Container container = ReadContainer(saveData);
        return _eventFlagSectionReader.Read(container, slotIndex);
    }

    private Bnd4Container ReadContainer(ReadOnlyMemory<byte> saveData)
    {
        Bnd4Container container = _bnd4Reader.Read(saveData);

        if (container.Entries.Count != ExpectedEntryCount)
        {
            throw new SaveParseException(
                SaveParseErrorCode.InvalidBnd4Header,
                $"The Elden Ring PC save contains {container.Entries.Count} BND4 entries; expected {ExpectedEntryCount}.",
                Bnd4Reader.FileCountOffset);
        }

        return container;
    }
}
