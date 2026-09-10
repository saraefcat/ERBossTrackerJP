using ERBossTrackerJP.Core.Models;

namespace ERBossTrackerJP.Save.Progress;

public interface IBossProgressService
{
    TrackerSnapshot CreateSnapshot(
        DateTimeOffset updatedAt,
        CharacterSlot character,
        ReadOnlyMemory<byte> eventFlags,
        IReadOnlyList<BossDefinition> bossDefinitions);
}
