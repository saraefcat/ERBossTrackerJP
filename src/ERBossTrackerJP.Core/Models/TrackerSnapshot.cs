namespace ERBossTrackerJP.Core.Models;

public sealed record TrackerSnapshot
{
    public TrackerSnapshot(
        DateTimeOffset updatedAt,
        CharacterSlot character,
        IEnumerable<BossProgress> bosses,
        IEnumerable<RegionProgress> regions,
        uint saveDeathCount = 0,
        uint deathCountBaseline = 0,
        bool isDeathCountOffsetEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(bosses);
        ArgumentNullException.ThrowIfNull(regions);

        BossProgress[] bossArray = bosses.ToArray();
        RegionProgress[] regionArray = regions.ToArray();

        UpdatedAt = updatedAt;
        Character = character;
        Bosses = Array.AsReadOnly(bossArray);
        Regions = Array.AsReadOnly(regionArray);
        Defeated = bossArray.Count(boss => boss.IsDefeated);
        Total = bossArray.Length;
        SaveDeathCount = saveDeathCount;
        DeathCountBaseline = deathCountBaseline;
        IsDeathCountOffsetEnabled = isDeathCountOffsetEnabled;
        CumulativeDeathCount = saveDeathCount;
        DisplayDeathCount = !isDeathCountOffsetEnabled
            ? saveDeathCount
            : saveDeathCount >= deathCountBaseline
                ? saveDeathCount - deathCountBaseline
                : 0;
    }

    public DateTimeOffset UpdatedAt { get; }

    public CharacterSlot Character { get; }

    public int Defeated { get; }

    public int Total { get; }

    public int Remaining => Total - Defeated;

    public double ProgressPercentage => Total == 0 ? 0 : Defeated * 100d / Total;

    public uint SaveDeathCount { get; }

    public uint DeathCountBaseline { get; }

    public bool IsDeathCountOffsetEnabled { get; }

    public ulong CumulativeDeathCount { get; }

    public uint DisplayDeathCount { get; }

    public bool IsDeathCountBaselineAboveCumulative =>
        IsDeathCountOffsetEnabled && DeathCountBaseline > SaveDeathCount;

    public IReadOnlyList<BossProgress> Bosses { get; }

    public IReadOnlyList<RegionProgress> Regions { get; }
}
