namespace ERBossTrackerJP.Core.Presentation;

public sealed record RegionListItem(
    string RegionId,
    string Name,
    int Defeated,
    int Total)
{
    public int Remaining => Total - Defeated;
}
