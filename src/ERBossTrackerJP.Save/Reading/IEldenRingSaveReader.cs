using ERBossTrackerJP.Core.Models;

namespace ERBossTrackerJP.Save.Reading;

public interface IEldenRingSaveReader
{
    IReadOnlyList<CharacterSlot> ReadCharacterSlots(ReadOnlyMemory<byte> saveData);

    EventFlagSection ReadEventFlags(ReadOnlyMemory<byte> saveData, int slotIndex);
}
