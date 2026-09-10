namespace ERBossTrackerJP.Core.Models;

public sealed record CharacterSlot
{
    public const int MaximumSlotCount = 10;

    public CharacterSlot(int slotIndex, string name, uint level)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slotIndex);

        if (slotIndex >= MaximumSlotCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotIndex),
                slotIndex,
                $"Slot index must be less than {MaximumSlotCount}.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        SlotIndex = slotIndex;
        Name = name;
        Level = level;
    }

    public int SlotIndex { get; }

    public string Name { get; }

    public uint Level { get; }
}
