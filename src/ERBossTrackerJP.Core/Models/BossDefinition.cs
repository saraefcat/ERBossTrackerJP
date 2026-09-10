namespace ERBossTrackerJP.Core.Models;

public sealed record BossDefinition(
    string Id,
    uint FlagId,
    string NameEn,
    string NameJa,
    string RegionId,
    string RegionEn,
    string RegionJa,
    string LocationEn,
    string LocationJa,
    GameContent Content,
    int SortOrder);
