namespace ERBossTrackerJP.Core.Models;

public sealed record TrackerSnapshot
{
    public TrackerSnapshot(
        DateTimeOffset updatedAt,
        CharacterSlot character,
        IEnumerable<BossProgress> bosses,
        IEnumerable<RegionProgress> regions)
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
    }

    public DateTimeOffset UpdatedAt { get; }

    public CharacterSlot Character { get; }

    public int Defeated { get; }

    public int Total { get; }

    public int Remaining => Total - Defeated;

    public double ProgressPercentage => Total == 0 ? 0 : Defeated * 100d / Total;

    public IReadOnlyList<BossProgress> Bosses { get; }

    public IReadOnlyList<RegionProgress> Regions { get; }
}
