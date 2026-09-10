using ERBossTrackerJP.Core.Models;

namespace ERBossTrackerJP.Core.Presentation;

public sealed class TrackerDisplayModel
{
    public TrackerDisplayModel(
        TrackerSnapshot source,
        IEnumerable<BossListItem> bosses,
        IEnumerable<RegionListItem> regions)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(bosses);
        ArgumentNullException.ThrowIfNull(regions);

        BossListItem[] bossArray = bosses.ToArray();
        RegionListItem[] regionArray = regions.ToArray();

        UpdatedAt = source.UpdatedAt;
        Character = source.Character;
        Defeated = source.Defeated;
        Total = source.Total;
        Bosses = Array.AsReadOnly(bossArray);
        Regions = Array.AsReadOnly(regionArray);
    }

    public DateTimeOffset UpdatedAt { get; }

    public CharacterSlot Character { get; }

    public int Defeated { get; }

    public int Total { get; }

    public int Remaining => Total - Defeated;

    public double ProgressPercentage => Total == 0 ? 0 : Defeated * 100d / Total;

    public IReadOnlyList<BossListItem> Bosses { get; }

    public IReadOnlyList<RegionListItem> Regions { get; }
}
