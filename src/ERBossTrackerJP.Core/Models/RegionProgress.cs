namespace ERBossTrackerJP.Core.Models;

public sealed record RegionProgress(
    string RegionId,
    string RegionEn,
    string RegionJa,
    int Defeated,
    int Total)
{
    public int Remaining => Total - Defeated;
}
