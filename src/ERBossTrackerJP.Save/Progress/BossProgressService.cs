using System.Collections.ObjectModel;
using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Save.EventFlags;

namespace ERBossTrackerJP.Save.Progress;

public sealed class BossProgressService : IBossProgressService
{
    private readonly IReadOnlyDictionary<uint, uint> _blockOffsets;
    private readonly EventFlagReader _eventFlagReader = new();

    public BossProgressService()
        : this(EmbeddedEventFlagBlockMap.Load())
    {
    }

    public BossProgressService(IReadOnlyDictionary<uint, uint> blockOffsets)
    {
        ArgumentNullException.ThrowIfNull(blockOffsets);

        _blockOffsets = new ReadOnlyDictionary<uint, uint>(
            new Dictionary<uint, uint>(blockOffsets));
    }

    public TrackerSnapshot CreateSnapshot(
        DateTimeOffset updatedAt,
        CharacterSlot character,
        ReadOnlyMemory<byte> eventFlags,
        IReadOnlyList<BossDefinition> bossDefinitions,
        uint saveDeathCount = 0,
        uint deathCountOffset = 0)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(bossDefinitions);

        if (bossDefinitions.Count == 0)
        {
            throw new ArgumentException(
                "At least one boss definition is required.",
                nameof(bossDefinitions));
        }

        BossDefinition[] orderedDefinitions = CopyAndOrderDefinitions(bossDefinitions);
        var bosses = new BossProgress[orderedDefinitions.Length];
        var regionIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
        var regionAccumulators = new List<RegionAccumulator>();

        for (int index = 0; index < orderedDefinitions.Length; index++)
        {
            BossDefinition definition = orderedDefinitions[index];
            bool isDefeated = _eventFlagReader.IsSet(
                eventFlags.Span,
                definition.FlagId,
                _blockOffsets);
            bosses[index] = new BossProgress(definition, isDefeated);

            if (!regionIndexes.TryGetValue(definition.RegionId, out int regionIndex))
            {
                regionIndex = regionAccumulators.Count;
                regionIndexes.Add(definition.RegionId, regionIndex);
                regionAccumulators.Add(new RegionAccumulator(definition));
            }

            regionAccumulators[regionIndex].Add(definition, isDefeated);
        }

        RegionProgress[] regions = regionAccumulators
            .Select(static accumulator => accumulator.ToProgress())
            .ToArray();

        return new TrackerSnapshot(
            updatedAt,
            character,
            bosses,
            regions,
            saveDeathCount,
            deathCountOffset);
    }

    private static BossDefinition[] CopyAndOrderDefinitions(
        IReadOnlyList<BossDefinition> bossDefinitions)
    {
        var orderedDefinitions = new BossDefinition[bossDefinitions.Count];

        for (int index = 0; index < bossDefinitions.Count; index++)
        {
            orderedDefinitions[index] = bossDefinitions[index] ??
                throw new ArgumentException(
                    $"Boss definition at index {index} is null.",
                    nameof(bossDefinitions));
        }

        Array.Sort(
            orderedDefinitions,
            static (left, right) => left.SortOrder.CompareTo(right.SortOrder));
        return orderedDefinitions;
    }

    private sealed class RegionAccumulator
    {
        private readonly GameContent _content;

        public RegionAccumulator(BossDefinition firstBoss)
        {
            RegionId = firstBoss.RegionId;
            RegionEn = firstBoss.RegionEn;
            RegionJa = firstBoss.RegionJa;
            _content = firstBoss.Content;
        }

        public string RegionId { get; }

        public string RegionEn { get; }

        public string RegionJa { get; }

        public int Defeated { get; private set; }

        public int Total { get; private set; }

        public void Add(BossDefinition boss, bool isDefeated)
        {
            if (!string.Equals(RegionEn, boss.RegionEn, StringComparison.Ordinal) ||
                !string.Equals(RegionJa, boss.RegionJa, StringComparison.Ordinal) ||
                _content != boss.Content)
            {
                throw new ArgumentException(
                    $"Region '{RegionId}' has inconsistent names or content.",
                    nameof(boss));
            }

            Total++;

            if (isDefeated)
            {
                Defeated++;
            }
        }

        public RegionProgress ToProgress() =>
            new(RegionId, RegionEn, RegionJa, Defeated, Total);
    }
}
