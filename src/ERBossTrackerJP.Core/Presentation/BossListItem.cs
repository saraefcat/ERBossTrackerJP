using ERBossTrackerJP.Core.Models;

namespace ERBossTrackerJP.Core.Presentation;

public sealed record BossListItem(
    string Id,
    uint FlagId,
    bool IsDefeated,
    string Name,
    string RegionId,
    string Region,
    string Location,
    GameContent Content,
    int SortOrder);
