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
    int SortOrder)
{
    public string StatusText => IsDefeated ? "撃破済み" : "未撃破";

    public string ContentText => Content switch
    {
        GameContent.BaseGame => "本編",
        GameContent.ShadowOfTheErdtree => "DLC",
        _ => throw new ArgumentOutOfRangeException(nameof(Content), Content, null),
    };
}
